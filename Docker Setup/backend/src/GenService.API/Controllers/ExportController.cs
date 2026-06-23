using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using GenService.API.Data;

namespace GenService.API.Controllers;

/// <summary>
/// Generates downloadable Excel (.xlsx) and PDF reports for all modules.
/// All endpoints share ?format=excel|pdf  (default = excel).
/// </summary>
[ApiController]
[Route("api/v1/export")]
[Authorize]
public class ExportController(GenServiceDbContext db, ILogger<ExportController> log) : ControllerBase
{
    // ── 1. Store Inventory ────────────────────────────────────────────────────
    [HttpGet("inventory")]
    public async Task<IActionResult> Inventory(
        [FromQuery] string? category = null,
        [FromQuery] bool?   lowStock = null,
        [FromQuery] string  format   = "excel")
    {
        var q = db.StoreItems.Where(x => x.IsActive).AsQueryable();
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(x => x.Category == category);
        if (lowStock == true) q = q.Where(x => x.QuantityInStock <= x.ReorderLevel);

        var items = await q.OrderBy(x => x.Category).ThenBy(x => x.Name).ToListAsync();

        var headers = new[] { "Code", "Name", "Category", "Unit", "Qty In Stock", "Reorder Level", "Low Stock?", "Unit Cost (₦)", "Total Value (₦)", "Store Location", "Supplier" };
        var rows = items.Select(i => new[]
        {
            i.ItemCode, i.Name, i.Category, i.Unit,
            i.QuantityInStock.ToString("G"),
            i.ReorderLevel.ToString("G"),
            i.QuantityInStock <= i.ReorderLevel ? "YES" : "",
            i.UnitCostNaira.ToString("N2"),
            ((decimal)i.QuantityInStock * i.UnitCostNaira).ToString("N2"),
            i.StoreLocation ?? "",
            i.Supplier ?? "",
        }).ToList();

        var summary = new[] { ("Total items", items.Count.ToString()), ("Low stock", items.Count(i => i.QuantityInStock <= i.ReorderLevel).ToString()), ("Total value", "₦" + items.Sum(i => (decimal)i.QuantityInStock * i.UnitCostNaira).ToString("N0")) };

        return format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? PdfResult("Store Inventory Report", "Desicon Group — General Service", summary, headers, rows, "Inventory", new float[] { 1.8f, 4.5f, 2.5f, 1.5f, 1.5f, 1.5f, 1.5f, 2.2f, 2.2f, 3f, 2.5f })
            : ExcelResult("Inventory", headers, rows, "Inventory_Report");
    }

    // ── 2. Store Movements ────────────────────────────────────────────────────
    [HttpGet("store-movements")]
    public async Task<IActionResult> StoreMovements(
        [FromQuery] string? itemCode = null,
        [FromQuery] string? type     = null,
        [FromQuery] string? from     = null,
        [FromQuery] string? to       = null,
        [FromQuery] string  format   = "excel")
    {
        var q = db.StoreMovements.AsQueryable();
        if (!string.IsNullOrWhiteSpace(itemCode)) q = q.Where(x => x.ItemCode == itemCode);
        if (!string.IsNullOrWhiteSpace(type))     q = q.Where(x => x.MovementType == type);
        if (DateTime.TryParse(from, out var f))   q = q.Where(x => x.CreatedAt >= f);
        if (DateTime.TryParse(to,   out var t))   q = q.Where(x => x.CreatedAt <= t.AddDays(1));

        var items = await q.OrderByDescending(x => x.CreatedAt).Take(5000).ToListAsync();

        var headers = new[] { "Date", "Item Code", "Item Name", "Movement Type", "Qty Before", "Qty Change", "Qty After", "Reference", "Notes", "By" };
        var rows = items.Select(m => new[]
        {
            m.CreatedAt.ToString("dd MMM yyyy HH:mm"),
            m.ItemCode, m.ItemName, m.MovementType,
            m.QuantityBefore.ToString("G"),
            (m.QuantityChange >= 0 ? "+" : "") + m.QuantityChange.ToString("G"),
            m.QuantityAfter.ToString("G"),
            m.Reference ?? "", m.Notes ?? "",
            m.MovedByName,
        }).ToList();

        return ExcelResult("StoreMovements", headers, rows, "Stock_Movement_Log");
    }

    // ── 3. Store Requisitions ──────────────────────────────────────────────────
    [HttpGet("requisitions")]
    public async Task<IActionResult> Requisitions(
        [FromQuery] string? status = null,
        [FromQuery] string? from   = null,
        [FromQuery] string? to     = null,
        [FromQuery] string  format = "excel")
    {
        var q = db.StoreRequisitions.Include(r => r.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))   q = q.Where(r => r.Status == status);
        if (DateTime.TryParse(from, out var f))   q = q.Where(r => r.CreatedAt >= f);
        if (DateTime.TryParse(to,   out var t))   q = q.Where(r => r.CreatedAt <= t.AddDays(1));

        var items = await q.OrderByDescending(r => r.CreatedAt).Take(2000).ToListAsync();

        var headers = new[] { "Req. No.", "Requested By", "Department", "Purpose", "Status", "Items", "Total Cost (₦)", "Submitted", "Approved By", "Issued By", "Rejection Reason" };
        var rows = items.Select(r => new[]
        {
            r.RequisitionNumber, r.RequestedByName, r.Department, r.Purpose,
            r.Status,
            r.Items.Count.ToString(),
            r.Items.Sum(i => (decimal)i.QuantityIssued * i.UnitCostNaira).ToString("N2"),
            r.CreatedAt.ToString("dd MMM yyyy"),
            r.ApprovedByName ?? "",
            r.IssuedByName   ?? "",
            r.RejectionReason ?? "",
        }).ToList();

        var summary = new[] {
            ("Total", items.Count.ToString()),
            ("Pending",  items.Count(r => r.Status == "Pending").ToString()),
            ("Approved", items.Count(r => r.Status == "Approved").ToString()),
            ("Issued",   items.Count(r => r.Status == "Issued").ToString()),
            ("Rejected", items.Count(r => r.Status == "Rejected").ToString()),
        };

        return format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? PdfResult("Store Requisitions Report", "Desicon Group — General Service", summary, headers, rows, "Requisitions", new float[] { 1.8f, 2.5f, 2f, 4f, 1.5f, 1f, 2.2f, 2f, 2.5f, 2.5f, 3f })
            : ExcelResult("Requisitions", headers, rows, "Requisitions_Report");
    }

    // ── 4. Vehicle Maintenance Register ───────────────────────────────────────
    [HttpGet("vehicle-register")]
    public async Task<IActionResult> VehicleRegister(
        [FromQuery] string? regNo  = null,
        [FromQuery] string? status = null,
        [FromQuery] string? from   = null,
        [FromQuery] string? to     = null,
        [FromQuery] string  format = "excel")
    {
        var q = db.VehicleMaintenanceRequests.AsQueryable();
        if (!string.IsNullOrWhiteSpace(regNo))  q = q.Where(v => v.VehicleRegNo.Contains(regNo));
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(v => v.Status == status);
        if (DateTime.TryParse(from, out var f)) q = q.Where(v => v.CreatedAt >= f);
        if (DateTime.TryParse(to,   out var t)) q = q.Where(v => v.CreatedAt <= t.AddDays(1));

        var items = await q.OrderByDescending(v => v.CreatedAt).Take(5000).ToListAsync();

        var headers = new[]
        {
            "Req. No.", "Reg No.", "Vehicle Type", "Maint. Type", "Description", "Status",
            "Actioned By", "Date Reported", "Date Completed", "Fault Identified",
            "Work Done", "Spares Cost (₦)", "Workshop", "Odometer", "Notes",
        };
        var rows = items.Select(v => new[]
        {
            v.RequestNumber, v.VehicleRegNo, v.VehicleType, v.MaintenanceType, v.Description, v.Status,
            v.ActionedBy ?? "",
            v.CreatedAt.ToString("dd MMM yyyy"),
            v.CompletedAt?.ToString("dd MMM yyyy") ?? "",
            v.FaultIdentified ?? "",
            v.WorkDone ?? "",
            v.SparesCostNaira?.ToString("N2") ?? "",
            v.WorkshopName ?? "",
            v.OdometerReading ?? "",
            v.Notes ?? "",
        }).ToList();

        var summary = new[]
        {
            ("Total jobs", items.Count.ToString()),
            ("Completed", items.Count(v => v.Status == "Completed").ToString()),
            ("Pending/In Progress", items.Count(v => v.Status != "Completed" && v.Status != "Cancelled").ToString()),
            ("Spares cost", "₦" + items.Sum(v => v.SparesCostNaira ?? 0).ToString("N0")),
        };

        return format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? PdfResult("Vehicle Maintenance Register", "Desicon Group — General Service", summary, headers, rows, "Vehicle_Register", new float[] { 1.8f, 1.8f, 2.5f, 2f, 3.5f, 1.8f, 2f, 2f, 2f, 3f, 3.5f, 2f, 2f, 1.8f, 2.5f })
            : ExcelResult("VehicleRegister", headers, rows, "Vehicle_Maintenance_Register");
    }

    // ── 5. Equipment Maintenance ──────────────────────────────────────────────
    [HttpGet("equipment-maintenance")]
    public async Task<IActionResult> EquipmentMaintenance(
        [FromQuery] string? status = null,
        [FromQuery] string? from   = null,
        [FromQuery] string? to     = null,
        [FromQuery] string  format = "excel")
    {
        var q = db.EquipmentMaintenanceRequests.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(e => e.Status == status);
        if (DateTime.TryParse(from, out var f)) q = q.Where(e => e.CreatedAt >= f);
        if (DateTime.TryParse(to,   out var t)) q = q.Where(e => e.CreatedAt <= t.AddDays(1));

        var items = await q.OrderByDescending(e => e.CreatedAt).Take(5000).ToListAsync();

        var headers = new[]
        {
            "Req. No.", "Asset", "Asset No.", "Maint. Type", "Location", "Description", "Status",
            "Actioned By", "Date Reported", "Date Completed",
            "Fault Identified", "Work Done", "Spares Cost (₦)", "Notes",
        };
        var rows = items.Select(e => new[]
        {
            e.RequestNumber, e.AssetDescription, e.AssetNo, e.MaintenanceType, e.Location, e.Description, e.Status,
            e.ActionedBy ?? "",
            e.CreatedAt.ToString("dd MMM yyyy"),
            e.CompletedAt?.ToString("dd MMM yyyy") ?? "",
            e.FaultIdentified ?? "",
            e.WorkDone ?? "",
            e.SparesCostNaira?.ToString("N2") ?? "",
            e.Notes ?? "",
        }).ToList();

        var summary = new[]
        {
            ("Total", items.Count.ToString()),
            ("Completed", items.Count(e => e.Status == "Completed").ToString()),
            ("Open/In Progress", items.Count(e => e.Status != "Completed" && e.Status != "Cancelled").ToString()),
            ("Spares cost", "₦" + items.Sum(e => e.SparesCostNaira ?? 0).ToString("N0")),
        };

        return format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? PdfResult("Equipment Maintenance Register", "Desicon Group — General Service", summary, headers, rows, "Equipment", new float[] { 1.8f, 3.5f, 1.5f, 2f, 2.5f, 3.5f, 1.8f, 2f, 2f, 2f, 3f, 3f, 2f, 2.5f })
            : ExcelResult("Equipment", headers, rows, "Equipment_Maintenance_Register");
    }

    // ── 6. Facility Maintenance ───────────────────────────────────────────────
    [HttpGet("facility-maintenance")]
    public async Task<IActionResult> FacilityMaintenance(
        [FromQuery] string? status = null,
        [FromQuery] string? from   = null,
        [FromQuery] string? to     = null,
        [FromQuery] string  format = "excel")
    {
        var q = db.FacilityMaintenanceRequests.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(fx => fx.Status == status);
        if (DateTime.TryParse(from, out var f)) q = q.Where(fx => fx.CreatedAt >= f);
        if (DateTime.TryParse(to,   out var t)) q = q.Where(fx => fx.CreatedAt <= t.AddDays(1));

        var items = await q.OrderByDescending(fx => fx.CreatedAt).Take(5000).ToListAsync();

        var headers = new[]
        {
            "Req. No.", "Maint. Type", "Location", "End User", "Description", "Status",
            "Actioned By", "Date Reported", "Date Completed",
            "Fault Identified", "Work Done", "Spares Cost (₦)", "Notes",
        };
        var rows = items.Select(fx => new[]
        {
            fx.RequestNumber, fx.MaintenanceType, fx.Location, fx.EndUser, fx.Description, fx.Status,
            fx.ActionedBy ?? "",
            fx.CreatedAt.ToString("dd MMM yyyy"),
            fx.CompletedAt?.ToString("dd MMM yyyy") ?? "",
            fx.FaultIdentified ?? "",
            fx.WorkDone ?? "",
            fx.SparesCostNaira?.ToString("N2") ?? "",
            fx.Notes ?? "",
        }).ToList();

        var summary = new[]
        {
            ("Total", items.Count.ToString()),
            ("Completed", items.Count(fx => fx.Status == "Completed").ToString()),
            ("Open/In Progress", items.Count(fx => fx.Status != "Completed" && fx.Status != "Cancelled").ToString()),
            ("Spares cost", "₦" + items.Sum(fx => fx.SparesCostNaira ?? 0).ToString("N0")),
        };

        return format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? PdfResult("Facility Maintenance Register", "Desicon Group — General Service", summary, headers, rows, "Facility", new float[] { 1.8f, 2.5f, 2.5f, 2f, 3.5f, 1.8f, 2f, 2f, 2f, 3f, 3f, 2f, 2.5f })
            : ExcelResult("Facility", headers, rows, "Facility_Maintenance_Register");
    }

    // ── 7. Daily Parameter Log ────────────────────────────────────────────────
    [HttpGet("daily-log")]
    public async Task<IActionResult> DailyLog(
        [FromQuery] string? location = null,
        [FromQuery] string? from     = null,
        [FromQuery] string? to       = null,
        [FromQuery] string  format   = "excel")
    {
        var q = db.DailyParameterLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(location)) q = q.Where(d => d.Location == location);
        if (DateOnly.TryParse(from, out var fd))  q = q.Where(d => d.LogDate >= fd);
        if (DateOnly.TryParse(to,   out var td))  q = q.Where(d => d.LogDate <= td);

        var items = await q.OrderByDescending(d => d.LogDate).Take(2000).ToListAsync();

        var headers = new[]
        {
            "Date", "Location",
            "NEPA Hrs", "Gen Hrs Run", "Diesel Consumed (L)", "Diesel Balance (L)", "Gen Status",
            "Water Source", "Tank Level %", "Water Status",
            "Staff Present", "Expected Staff", "Visitors",
            "Cleaning Done", "Waste Disposed", "Security Status",
            "Maintenance Issues", "Actions Taken", "Pending Actions",
            "Logged By",
        };
        var rows = items.Select(d => new[]
        {
            d.LogDate.ToString("dd MMM yyyy"), d.Location,
            d.NepaHoursAvailable?.ToString("G") ?? "", d.GeneratorHoursRun?.ToString("G") ?? "",
            d.DieselConsumedLitres?.ToString("G") ?? "", d.DieselBalanceLitres?.ToString("G") ?? "",
            d.GeneratorStatus ?? "",
            d.WaterSource ?? "", d.WaterTankLevelPercent?.ToString() ?? "", d.WaterStatus ?? "",
            d.StaffPresent?.ToString() ?? "", d.ExpectedStaff?.ToString() ?? "", d.VisitorCount?.ToString() ?? "",
            d.CleaningDone == true ? "Yes" : d.CleaningDone == false ? "No" : "",
            d.WasteDisposed == true ? "Yes" : d.WasteDisposed == false ? "No" : "",
            d.SecurityStatus ?? "",
            d.MaintenanceIssues ?? "", d.ActionsTaken ?? "", d.PendingActions ?? "",
            d.LoggedByName,
        }).ToList();

        var summary = new[]
        {
            ("Total entries", items.Count.ToString()),
            ("Avg NEPA hrs", items.Any(d => d.NepaHoursAvailable.HasValue) ? items.Where(d => d.NepaHoursAvailable.HasValue).Average(d => d.NepaHoursAvailable!.Value).ToString("N1") : "N/A"),
            ("Avg diesel/day", items.Any(d => d.DieselConsumedLitres.HasValue) ? items.Where(d => d.DieselConsumedLitres.HasValue).Average(d => d.DieselConsumedLitres!.Value).ToString("N1") + " L" : "N/A"),
        };

        return format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? PdfResult("Daily Parameter Log Report", "Desicon Group — General Service", summary, headers, rows, "Daily Log", new float[] { 2f, 2.5f, 1.5f, 1.5f, 2f, 2f, 2f, 2f, 1.5f, 2f, 1.5f, 1.5f, 1.5f, 1.5f, 1.5f, 2f, 3f, 3f, 2.5f, 2.5f })
            : ExcelResult("DailyLog", headers, rows, "Daily_Parameter_Log");
    }

    // ── 8. Diesel Requisitions ────────────────────────────────────────────────
    [HttpGet("diesel-requisitions")]
    public async Task<IActionResult> DieselRequisitions(
        [FromQuery] string? status = null,
        [FromQuery] string? from   = null,
        [FromQuery] string? to     = null,
        [FromQuery] string  format = "excel")
    {
        var q = db.DieselRequisitions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))   q = q.Where(r => r.Status == status);
        if (DateTime.TryParse(from, out var f))   q = q.Where(r => r.CreatedAt >= f);
        if (DateTime.TryParse(to,   out var t))   q = q.Where(r => r.CreatedAt <= t.AddDays(1));

        var items = await q.OrderByDescending(r => r.CreatedAt).Take(2000).ToListAsync();

        var headers = new[]
        {
            "Req. No.", "Purpose", "Equip. Type", "Reference", "Location",
            "Qty Requested (L)", "Requested By", "Department", "Status",
            "Approved By", "Approved Date",
            "Dispensed By", "Dispensed Date", "Qty Dispensed (L)",
            "Tank Before (L)", "Tank After (L)", "Unit Cost (₦/L)", "Total Cost (₦)",
            "Rejection Reason", "Notes",
        };
        var rows = items.Select(r => new[]
        {
            r.RequisitionNumber, r.Purpose, r.EquipmentType, r.EquipmentReference ?? "", r.Location,
            r.QuantityRequestedLitres.ToString("G"),
            r.RequestedByName, r.Department, r.Status,
            r.ApprovedByName ?? "", r.ApprovedAt?.ToString("dd MMM yyyy") ?? "",
            r.DispensedByName ?? "", r.DispensedAt?.ToString("dd MMM yyyy") ?? "",
            r.QuantityDispensedLitres?.ToString("G") ?? "",
            r.TankLevelBeforeLitres?.ToString("G") ?? "",
            r.TankLevelAfterLitres?.ToString("G")  ?? "",
            r.UnitCostPerLitreNaira?.ToString("N4") ?? "",
            r.TotalCostNaira?.ToString("N2") ?? "",
            r.RejectionReason ?? "", r.Notes ?? "",
        }).ToList();

        var summary = new[]
        {
            ("Total", items.Count.ToString()),
            ("Pending",   items.Count(r => r.Status == "Pending").ToString()),
            ("Approved",  items.Count(r => r.Status == "Approved").ToString()),
            ("Dispensed", items.Count(r => r.Status == "Dispensed").ToString()),
            ("Rejected",  items.Count(r => r.Status == "Rejected").ToString()),
            ("Total Litres", items.Where(r => r.Status == "Dispensed").Sum(r => r.QuantityDispensedLitres ?? 0).ToString("N0") + " L"),
            ("Total Cost", "₦" + items.Where(r => r.Status == "Dispensed").Sum(r => r.TotalCostNaira ?? 0).ToString("N0")),
        };

        return format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? PdfResult("Diesel Requisitions Report", "Desicon Group — General Service", summary, headers, rows, "Diesel_Requisitions", new float[] { 1.8f, 4f, 2f, 2f, 2f, 2f, 2.5f, 2f, 1.8f, 2.5f, 2f, 2.5f, 2f, 2f, 2f, 2f, 2.2f, 2.2f, 3f, 3f })
            : ExcelResult("DieselRequisitions", headers, rows, "Diesel_Requisitions_Report");
    }

    // ── 9. Generator / Power Log (Excel only) ─────────────────────────────────
    [HttpGet("generator-log")]
    public async Task<IActionResult> GeneratorLog(
        [FromQuery] string? location = null,
        [FromQuery] string? assetNo  = null,
        [FromQuery] string? from     = null,
        [FromQuery] string? to       = null,
        [FromQuery] string  format   = "excel")
    {
        var q = db.GeneratorDailyReadings.AsQueryable();
        if (!string.IsNullOrWhiteSpace(location)) q = q.Where(g => g.Location == location);
        if (!string.IsNullOrWhiteSpace(assetNo))  q = q.Where(g => g.AssetNo == assetNo);
        if (DateTime.TryParse(from, out var f))   q = q.Where(g => g.ReadingDate >= f);
        if (DateTime.TryParse(to,   out var t))   q = q.Where(g => g.ReadingDate <= t.AddDays(1));

        var items = await q.OrderByDescending(g => g.ReadingDate).Take(2000).ToListAsync();

        var headers = new[] { "Date", "Asset No.", "Generator", "Location", "Status", "Run Hours Today", "Cumulative Hours", "Fuel Consumed (L)", "Utility Hours", "Notes", "Logged By" };
        var rows = items.Select(g => new[]
        {
            g.ReadingDate.ToString("dd MMM yyyy"),
            g.AssetNo,
            g.AssetDescription,
            g.Location,
            g.GeneratorStatus,
            g.RunHoursToday.ToString("G"),
            g.CumulativeRunHours.ToString("G"),
            g.FuelConsumedLitres?.ToString("G") ?? "",
            g.UtilityAvailableHours?.ToString("G") ?? "",
            g.Notes ?? "",
            g.LoggedByName,
        }).ToList();

        return ExcelResult("GeneratorLog", headers, rows, "Generator_Daily_Log");
    }

    // ── 10. Maintenance Schedules ─────────────────────────────────────────────
    [HttpGet("maintenance-schedules")]
    public async Task<IActionResult> MaintenanceSchedules(
        [FromQuery] string? category = null,
        [FromQuery] string? status   = null,   // "overdue" | "due-soon" | "active" | "all"
        [FromQuery] string  format   = "excel")
    {
        var now   = DateTime.UtcNow;
        var soon  = now.AddDays(7);

        var q = db.MaintenanceSchedules.AsQueryable();
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(m => m.Category == category);

        q = status switch
        {
            "overdue"  => q.Where(m => m.IsActive && m.NextDueAt < now),
            "due-soon" => q.Where(m => m.IsActive && m.NextDueAt >= now && m.NextDueAt <= soon),
            "active"   => q.Where(m => m.IsActive),
            _          => q,   // "all" — include inactive too
        };

        var items = await q
            .OrderByDescending(m => m.EscalationLevel)
            .ThenBy(m => m.NextDueAt)
            .Take(2000)
            .ToListAsync();

        var headers = new[]
        {
            "Task Name", "Category", "Group", "Location", "Frequency",
            "Next Due", "Days Until Due", "Status",
            "Escalation Level",
            "Last Completed", "Last Completed By", "Completion Notes",
            "Assigned To",
            "Last Reminder Sent", "Last Escalation Sent",
            "Active?",
        };

        var rows = items.Select(m =>
        {
            var daysUntil = (int)Math.Round((m.NextDueAt - now).TotalDays);
            var statusLabel = m.NextDueAt < now
                ? $"OVERDUE ({Math.Abs(daysUntil)}d)"
                : daysUntil == 0
                    ? "Due Today"
                    : daysUntil <= 7
                        ? $"Due Soon ({daysUntil}d)"
                        : $"OK ({daysUntil}d)";

            var escalation = m.EscalationLevel switch
            {
                0 => "None",
                1 => "Supervisor Notified",
                2 => "Manager Notified",
                _ => $"Level {m.EscalationLevel}",
            };

            return new[]
            {
                m.TaskName,
                m.Category,
                GenService.API.Domain.MaintenanceGroup.ForCategory(m.Category),
                m.Location,
                m.FrequencyLabel,
                m.NextDueAt.ToString("dd MMM yyyy"),
                daysUntil.ToString(),
                statusLabel,
                escalation,
                m.LastCompletedAt?.ToString("dd MMM yyyy") ?? "",
                m.LastCompletedByName ?? "",
                m.LastCompletionNotes ?? "",
                m.AssignedToName ?? "",
                m.LastReminderSentAt?.ToString("dd MMM yyyy HH:mm") ?? "",
                m.LastEscalationSentAt?.ToString("dd MMM yyyy HH:mm") ?? "",
                m.IsActive ? "Yes" : "No",
            };
        }).ToList();

        var overdue    = items.Count(m => m.NextDueAt < now);
        var dueSoon    = items.Count(m => m.NextDueAt >= now && m.NextDueAt <= soon);
        var escalated  = items.Count(m => m.EscalationLevel > 0);
        var completed  = items.Count(m => m.LastCompletedAt.HasValue);

        var summary = new[]
        {
            ("Total schedules",   items.Count.ToString()),
            ("Active",            items.Count(m => m.IsActive).ToString()),
            ("Overdue",           overdue.ToString()),
            ("Due within 7 days", dueSoon.ToString()),
            ("Escalated",         escalated.ToString()),
            ("Ever completed",    completed.ToString()),
            ("Generated",         now.ToString("dd MMM yyyy HH:mm") + " UTC"),
        };

        return format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? PdfResult("Maintenance Schedule Report", "Desicon Group — General Service",
                summary, headers, rows, "Maintenance_Schedules",
                new float[] { 4f, 2.5f, 1.8f, 2.5f, 1.8f, 2f, 1.5f, 2.2f, 2.5f, 2f, 2.5f, 3.5f, 2.5f, 2.5f, 2.5f, 1.2f })
            : ExcelResult("MaintenanceSchedules", headers, rows, "Maintenance_Schedules_Report");
    }

    // ── Excel builder ─────────────────────────────────────────────────────────
    private FileContentResult ExcelResult(string sheetName, string[] headers, List<string[]> rows, string fileNameBase)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(sheetName);

        // Header row
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1677ff");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Data rows
        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (int c = 0; c < row.Length; c++)
                ws.Cell(r + 2, c + 1).Value = row[c];

            if (r % 2 == 1)
                ws.Row(r + 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#f5f5f5");
        }

        ws.Columns().AdjustToContents(8, 60);
        ws.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var filename = $"{fileNameBase}_{DateTime.UtcNow:yyyyMMdd}.xlsx";
        log.LogInformation("📊 Excel export: {Sheet}, {Rows} rows", sheetName, rows.Count);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
    }

    // ── PDF builder ───────────────────────────────────────────────────────────
    private FileContentResult PdfResult(
        string title,
        string subtitle,
        (string Label, string Value)[] summary,
        string[] headers,
        List<string[]> rows,
        string sheetName,
        float[] colWidths)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily("Helvetica"));

                // Header band
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(inner =>
                        {
                            inner.Item().Text(title).Bold().FontSize(14).FontColor("#1677ff");
                            inner.Item().Text(subtitle).FontSize(9).FontColor("#595959");
                        });
                        row.ConstantItem(200).AlignRight().Column(inner =>
                        {
                            inner.Item().Text($"Generated: {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC").FontSize(8).FontColor("#8c8c8c");
                            inner.Item().Text($"Total records: {rows.Count}").FontSize(8).FontColor("#8c8c8c");
                        });
                    });

                    // Summary KPI band
                    if (summary.Length > 0)
                    {
                        col.Item().PaddingTop(6).Row(row =>
                        {
                            foreach (var (label, value) in summary)
                            {
                                row.RelativeItem().Border(1).BorderColor("#d9d9d9").Padding(4).Column(inner =>
                                {
                                    inner.Item().Text(label).FontSize(7).FontColor("#8c8c8c");
                                    inner.Item().Text(value).Bold().FontSize(9);
                                });
                            }
                        });
                    }

                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#1677ff");
                });

                // Footer
                page.Footer().AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ").FontSize(7).FontColor("#8c8c8c");
                        x.CurrentPageNumber().FontSize(7).FontColor("#8c8c8c");
                        x.Span(" of ").FontSize(7).FontColor("#8c8c8c");
                        x.TotalPages().FontSize(7).FontColor("#8c8c8c");
                        x.Span(" — Desicon Group General Service Management Platform").FontSize(7).FontColor("#8c8c8c");
                    });

                // Content — data table
                page.Content().PaddingTop(8).Table(table =>
                {
                    // Column proportions
                    table.ColumnsDefinition(cd =>
                    {
                        foreach (var w in colWidths)
                            cd.RelativeColumn(w);
                    });

                    // Header
                    table.Header(header =>
                    {
                        for (int c = 0; c < headers.Length; c++)
                        {
                            header.Cell().Background("#1677ff").Padding(4)
                                .Text(headers[c]).Bold().FontColor(Colors.White).FontSize(7.5f);
                        }
                    });

                    // Rows
                    for (int r = 0; r < rows.Count; r++)
                    {
                        string bg = r % 2 == 0 ? Colors.White : "#fafafa";
                        foreach (var cell in rows[r])
                        {
                            table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#f0f0f0").Padding(3)
                                .Text(cell).FontSize(7.5f);
                        }
                    }
                });
            });
        });

        var bytes = doc.GeneratePdf();
        var filename = $"{sheetName.Replace(" ", "_")}_{DateTime.UtcNow:yyyyMMdd}.pdf";
        log.LogInformation("📄 PDF export: {Title}, {Rows} rows", title, rows.Count);
        return File(bytes, "application/pdf", filename);
    }
}
