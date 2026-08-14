using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using GenService.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/equipment-maintenance")]
[Authorize]
public class EquipmentMaintenanceController(
    GenServiceDbContext db,
    NotificationService notify,
    ILogger<EquipmentMaintenanceController> logger) : ControllerBase
{
    private string CallerEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";
    private string CallerName  => User.FindFirstValue(ClaimTypes.Name)  ?? "";
    private string CallerRole  => User.FindFirstValue(ClaimTypes.Role)  ?? "";

    private static EquipmentMaintenanceDto ToDto(EquipmentMaintenanceRequest r) => new(
        r.Id, r.RequestNumber,
        r.AssetNo, r.AssetDescription, r.MaintenanceType,
        r.EndUser, r.Location, r.RunningHours, r.NextServiceHour,
        r.Description, r.Priority, r.Status,
        r.RequestedByEmail, r.RequestedByName,
        r.ApprovedByEmail, r.ApprovedByName, r.ApprovedAt, r.RejectionReason,
        // assessment
        r.FaultIdentified, r.ProposedSolution, r.ResolutionType,
        // parts
        r.PartsRequired, r.PartsSource, r.ProcurementMethod, r.SparesCostNaira,
        // completion
        r.WorkDone, r.ActionedBy, r.CompletedAt,
        // handover
        r.HandoverConfirmed, r.DateHandedOver, r.HandedOverBy,
        r.Notes, r.CreatedAt, r.UpdatedAt,
        (int)(DateTime.UtcNow - r.CreatedAt).TotalDays,
        // register fields (July 2026)
        r.DateOfRequest, r.Requestor, r.NotificationStatus, r.OdometerReading,
        r.PartsSuppliedBy, r.DateTakenToWorkshop, r.JustificationEvaluation, r.DaysTakenToComplete,
        r.AmountNaira, r.FinalAmountNaira
    );

    private async Task<string> NextRefAsync()
    {
        var yr    = DateTime.UtcNow.Year % 100;
        var count = await db.EquipmentMaintenanceRequests
                            .CountAsync(r => r.CreatedAt.Year == DateTime.UtcNow.Year);
        return $"E/{yr}/{(count + 1):D3}";
    }

    // ── GET /api/v1/equipment-maintenance ────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<EquipmentMaintenanceListResponse>> List(
        [FromQuery] EquipmentQuery q)
    {
        var query = db.EquipmentMaintenanceRequests.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Status))
            query = query.Where(r => r.Status == q.Status);
        if (!string.IsNullOrWhiteSpace(q.Type))
            query = query.Where(r => r.MaintenanceType == q.Type);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.ToLower();
            query = query.Where(r =>
                r.RequestNumber.ToLower().Contains(s)     ||
                r.AssetNo.ToLower().Contains(s)           ||
                r.AssetDescription.ToLower().Contains(s)  ||
                r.Description.ToLower().Contains(s)       ||
                r.EndUser.ToLower().Contains(s)           ||
                r.RequestedByName.ToLower().Contains(s));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return Ok(new EquipmentMaintenanceListResponse(
            items.Select(ToDto), total, q.Page, q.PageSize));
    }

    // ── GET /api/v1/equipment-maintenance/stats ──────────────────────────────
    [HttpGet("stats")]
    public async Task<ActionResult<EquipmentMaintenanceStatsDto>> Stats()
    {
        var now        = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var all        = await db.EquipmentMaintenanceRequests.ToListAsync();

        return Ok(new EquipmentMaintenanceStatsDto(
            all.Count(r => r.Status == MaintenanceRequestStatus.Pending),
            all.Count(r => r.Status == MaintenanceRequestStatus.Approved),
            all.Count(r => r.Status == MaintenanceRequestStatus.Ongoing),
            all.Count(r => r.Status == MaintenanceRequestStatus.AwaitingSpares),
            all.Count(r => r.Status == MaintenanceRequestStatus.AwaitingFunds),
            all.Count(r => r.Status == MaintenanceRequestStatus.Completed && r.CompletedAt >= monthStart),
            all.Count(r => r.Status == MaintenanceRequestStatus.Rejected)
        ));
    }

    // ── GET /api/v1/equipment-maintenance/{id} ───────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EquipmentMaintenanceDto>> GetById(Guid id)
    {
        var r = await db.EquipmentMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound();
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/equipment-maintenance ───────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<EquipmentMaintenanceDto>> Create(
        [FromBody] CreateEquipmentMaintenanceRequest req)
    {
        var r = new EquipmentMaintenanceRequest
        {
            RequestNumber    = await NextRefAsync(),
            AssetNo          = req.AssetNo.Trim(),
            AssetDescription = req.AssetDescription.Trim(),
            MaintenanceType  = req.MaintenanceType,
            EndUser          = req.EndUser.Trim(),
            Location         = req.Location.Trim(),
            Description      = req.Description.Trim(),
            Priority         = req.Priority,
            RunningHours     = req.RunningHours,
            NextServiceHour  = req.NextServiceHour,
            DateOfRequest      = req.DateOfRequest?.Date ?? DateTime.UtcNow.Date,
            Requestor          = req.Requestor?.Trim(),
            NotificationStatus = req.NotificationStatus?.Trim(),
            OdometerReading    = req.OdometerReading?.Trim(),
            AmountNaira        = req.AmountNaira,
            RequestedByEmail = CallerEmail,
            RequestedByName  = CallerName,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow,
        };
        db.EquipmentMaintenanceRequests.Add(r);
        await db.SaveChangesAsync();

        // Alert management that a new request is awaiting approval.
        await notify.CreateAsync(
            title:      "🔧 Equipment maintenance awaiting approval",
            message:    $"{r.RequestNumber} — {r.AssetDescription} at {r.Location}, raised by {r.RequestedByName}. {r.Description}",
            type:       NotificationType.MaintenancePending,
            module:     "EquipmentMaintenance",
            entityId:   r.Id.ToString(),
            refNumber:  r.RequestNumber,
            targetRole: NotificationTarget.Management);

        return CreatedAtAction(nameof(GetById), new { id = r.Id }, ToDto(r));
    }

    // ── POST /api/v1/equipment-maintenance/{id}/approve ──────────────────────
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<EquipmentMaintenanceDto>> Approve(
        Guid id, [FromBody] ApproveEquipmentRequest req)
    {
        if (CallerRole is not ("DepartmentManager" or "Supervisor" or "SystemAdmin")) return Forbid();
        var r = await db.EquipmentMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound();
        if (r.Status != MaintenanceRequestStatus.Pending)
            return BadRequest(new { message = "Only Pending requests can be approved." });

        r.Status          = MaintenanceRequestStatus.Approved;
        r.ApprovedByEmail = CallerEmail;
        r.ApprovedByName  = CallerName;
        r.ApprovedAt      = DateTime.UtcNow;
        r.Notes           = req.Notes ?? r.Notes;
        r.UpdatedAt       = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await notify.CreateAsync(
            title:       "✅ Equipment maintenance approved",
            message:     $"{r.RequestNumber} ({r.AssetDescription}) was approved by {CallerName}.",
            type:        NotificationType.GsApproved,
            module:      "EquipmentMaintenance",
            entityId:    r.Id.ToString(),
            refNumber:   r.RequestNumber,
            targetRole:  NotificationTarget.Requester,
            targetEmail: r.RequestedByEmail);

        return Ok(ToDto(r));
    }

    // ── POST /api/v1/equipment-maintenance/{id}/reject ───────────────────────
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<EquipmentMaintenanceDto>> Reject(
        Guid id, [FromBody] RejectEquipmentRequest req)
    {
        if (CallerRole is not ("DepartmentManager" or "Supervisor" or "SystemAdmin")) return Forbid();
        var r = await db.EquipmentMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound();
        if (r.Status != MaintenanceRequestStatus.Pending)
            return BadRequest(new { message = "Only Pending requests can be rejected." });

        r.Status          = MaintenanceRequestStatus.Rejected;
        r.ApprovedByEmail = CallerEmail;
        r.ApprovedByName  = CallerName;
        r.ApprovedAt      = DateTime.UtcNow;
        r.RejectionReason = req.Reason?.Trim();
        r.UpdatedAt       = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await notify.CreateAsync(
            title:       "❌ Equipment maintenance rejected",
            message:     $"{r.RequestNumber} ({r.AssetDescription}) was rejected by {CallerName}. Reason: {r.RejectionReason}",
            type:        NotificationType.GsRejected,
            module:      "EquipmentMaintenance",
            entityId:    r.Id.ToString(),
            refNumber:   r.RequestNumber,
            targetRole:  NotificationTarget.Requester,
            targetEmail: r.RequestedByEmail);

        return Ok(ToDto(r));
    }

    // ── POST /api/v1/equipment-maintenance/{id}/assess ───────────────────────
    [HttpPost("{id:guid}/assess")]
    public async Task<ActionResult<EquipmentMaintenanceDto>> Assess(
        Guid id, [FromBody] EquipmentAssessmentRequest req)
    {
        var r = await db.EquipmentMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound();

        r.FaultIdentified  = req.FaultIdentified?.Trim();
        r.ProposedSolution = req.ProposedSolution?.Trim();
        r.ResolutionType   = req.ResolutionType;
        r.PartsRequired    = req.PartsRequired;
        r.PartsSource      = req.PartsSource;
        r.ProcurementMethod= req.ProcurementMethod;
        r.SparesCostNaira  = req.SparesCostNaira;

        if (req.PartsRequired && req.PartsSource == "NewPurchase" &&
            r.Status == MaintenanceRequestStatus.Ongoing)
        {
            r.Status = req.ProcurementMethod == "CashAdvance"
                ? MaintenanceRequestStatus.AwaitingFunds
                : MaintenanceRequestStatus.AwaitingSpares;
        }

        r.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ToDto(r));
    }

    // ── PATCH /api/v1/equipment-maintenance/{id}/status ──────────────────────
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<EquipmentMaintenanceDto>> UpdateStatus(
        Guid id, [FromBody] UpdateStatusRequest req)
    {
        var r = await db.EquipmentMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound();

        r.Status    = req.Status;
        r.UpdatedAt = DateTime.UtcNow;
        if (req.Status == MaintenanceRequestStatus.Completed)
            r.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/equipment-maintenance/{id}/complete ─────────────────────
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<EquipmentMaintenanceDto>> Complete(
        Guid id, [FromBody] CompleteEquipmentRequest req)
    {
        if (CallerRole is not ("DepartmentManager" or "Supervisor" or "SystemAdmin" or "Technician"))
            return Forbid();
        var r = await db.EquipmentMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound();

        // A request may only be completed once work is under way — never straight from
        // Pending/Approved, and not if already Completed or Rejected.
        var completable = new[]
        {
            MaintenanceRequestStatus.Ongoing,
            MaintenanceRequestStatus.AwaitingSpares,
            MaintenanceRequestStatus.AwaitingFunds,
        };
        if (!completable.Contains(r.Status))
            return BadRequest(new { message = "A request can only be marked Completed once work is Ongoing (or awaiting spares/funds). It cannot be completed while Pending, Approved, Rejected, or already Completed." });

        r.Status          = MaintenanceRequestStatus.Completed;
        r.WorkDone        = req.WorkDone?.Trim();
        r.ActionedBy      = req.ActionedBy?.Trim() ?? CallerName;
        r.SparesCostNaira = req.SparesCostNaira ?? r.SparesCostNaira;
        r.Notes           = req.Notes ?? r.Notes;
        r.PartsSuppliedBy         = req.PartsSuppliedBy?.Trim() ?? r.PartsSuppliedBy;
        r.DateTakenToWorkshop     = req.DateTakenToWorkshop?.Date ?? r.DateTakenToWorkshop;
        r.JustificationEvaluation = req.JustificationEvaluation?.Trim() ?? r.JustificationEvaluation;
        if (!string.IsNullOrWhiteSpace(req.NotificationStatus)) r.NotificationStatus = req.NotificationStatus.Trim();
        r.FinalAmountNaira = req.FinalAmountNaira ?? r.FinalAmountNaira;
        r.CompletedAt     = req.DateCompleted?.Date ?? DateTime.UtcNow;
        r.UpdatedAt       = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/equipment-maintenance/{id}/handover ─────────────────────
    [HttpPost("{id:guid}/handover")]
    public async Task<ActionResult<EquipmentMaintenanceDto>> Handover(
        Guid id, [FromBody] EquipmentHandoverRequest req)
    {
        var r = await db.EquipmentMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound();
        if (r.Status != MaintenanceRequestStatus.Completed)
            return BadRequest(new { message = "Handover can only be confirmed after completion." });

        r.HandoverConfirmed = true;
        r.HandedOverBy      = req.HandedOverBy.Trim();
        r.DateHandedOver    = req.DateHandedOver ?? DateTime.UtcNow;
        r.UpdatedAt         = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ToDto(r));
    }
}
