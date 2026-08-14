using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenService.API.Controllers;

// ── Report Explorer shared shapes ───────────────────────────────────────────────
public record ExplorerColumn(string Key, string Label, string Kind); // kind: text | date | money | number

public record ExplorerRowInternal(
    Dictionary<string, object?> Row,
    DateTime? Date,
    string?   Location,
    string?   Status,
    string?   Type,
    decimal?  Amount,
    string    Search);

[ApiController]
[Route("api/v1/reports")]
[Authorize]
public class ReportsController(GenServiceDbContext db) : ControllerBase
{
    // ── Period helper ─────────────────────────────────────────────────────────
    private static (DateTime From, string Label) ResolvePeriod(string period) => period switch
    {
        "7d"  => (DateTime.UtcNow.AddDays(-7),  "Last 7 Days"),
        "90d" => (DateTime.UtcNow.AddDays(-90), "Last 90 Days"),
        _     => (DateTime.UtcNow.AddDays(-30), "Last 30 Days"),   // default 30d
    };

    // ══════════════════════════════════════════════════════════════════════════
    //  GET /api/v1/reports/requests?period=30d
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("requests")]
    public async Task<ActionResult<RequestReportDto>> RequestReport(
        [FromQuery] string period = "30d")
    {
        var (from, label) = ResolvePeriod(period);

        var requests = await db.ServiceRequests
            .AsNoTracking()
            .Where(r => r.CreatedAt >= from)
            .ToListAsync();

        var total     = requests.Count;
        var completed = requests.Count(r => r.Status == RequestStatus.Completed);
        var rejected  = requests.Count(r => r.Status == RequestStatus.Rejected);
        var cancelled = requests.Count(r => r.Status == RequestStatus.Cancelled);
        var closed    = completed + rejected + cancelled;
        var rate      = closed > 0 ? Math.Round((double)completed / closed * 100, 1) : 0;

        // By category
        var byCategory = requests
            .GroupBy(r => r.Category)
            .OrderByDescending(g => g.Count())
            .Select(g => new PeriodBreakdownItem(g.Key, g.Count()))
            .ToList();

        // By status
        var byStatus = requests
            .GroupBy(r => r.Status)
            .OrderByDescending(g => g.Count())
            .Select(g => new PeriodBreakdownItem(g.Key, g.Count()))
            .ToList();

        // By priority
        var byPriority = requests
            .GroupBy(r => r.Priority)
            .OrderByDescending(g => g.Count())
            .Select(g => new PeriodBreakdownItem(g.Key, g.Count()))
            .ToList();

        // Daily submission trend
        var trend = requests
            .GroupBy(r => r.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new TrendPoint(g.Key.ToString("MMM dd"), g.Count()))
            .ToList();

        // Top requesters
        var topRequesters = requests
            .GroupBy(r => r.RequestedByName)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new PeriodBreakdownItem(g.Key, g.Count()))
            .ToList();

        return Ok(new RequestReportDto(
            total,
            requests.Count(r => r.Status == RequestStatus.Open),
            completed,
            requests.Count(r => r.Status == RequestStatus.PendingApproval),
            rejected,
            rate,
            byCategory, byStatus, byPriority,
            trend,
            topRequesters,
            label
        ));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  GET /api/v1/reports/maintenance?period=30d
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("maintenance")]
    public async Task<ActionResult<MaintenanceReportDto>> MaintenanceReport(
        [FromQuery] string period = "30d")
    {
        var (from, label) = ResolvePeriod(period);
        var now = DateTime.UtcNow;

        var allActive = await db.MaintenanceSchedules
            .AsNoTracking()
            .Where(m => m.IsActive)
            .ToListAsync();

        var completedThisPeriod = allActive
            .Where(m => m.LastCompletedAt.HasValue && m.LastCompletedAt.Value >= from)
            .ToList();

        var overdue  = allActive.Count(m => m.NextDueAt < now);
        var dueSoon  = allActive.Count(m => m.NextDueAt >= now && m.NextDueAt <= now.AddDays(7));
        var dueInPeriod = allActive.Count(m => m.NextDueAt >= from && m.NextDueAt <= now);
        var rate     = dueInPeriod > 0
            ? Math.Round((double)completedThisPeriod.Count / dueInPeriod * 100, 1)
            : 100.0;

        var byCategory = allActive
            .GroupBy(m => m.Category)
            .OrderByDescending(g => g.Count())
            .Select(g => new PeriodBreakdownItem(g.Key, g.Count()))
            .ToList();

        var byFrequency = allActive
            .GroupBy(m => m.FrequencyLabel)
            .OrderByDescending(g => g.Count())
            .Select(g => new PeriodBreakdownItem(g.Key, g.Count()))
            .ToList();

        var recentCompletions = completedThisPeriod
            .OrderByDescending(m => m.LastCompletedAt)
            .Take(8)
            .Select(m => new MaintenanceCompletionItem(
                m.TaskName, m.Category, m.Location,
                m.LastCompletedAt!.Value, m.LastCompletedByName))
            .ToList();

        return Ok(new MaintenanceReportDto(
            allActive.Count,
            overdue,
            completedThisPeriod.Count,
            dueSoon,
            rate,
            byCategory,
            byFrequency,
            recentCompletions,
            label
        ));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  GET /api/v1/reports/fuel?period=30d
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("fuel")]
    public async Task<ActionResult<FuelPowerReportDto>> FuelReport(
        [FromQuery] string period = "30d")
    {
        var (from, label) = ResolvePeriod(period);

        var genLogs = await db.GeneratorLogs
            .AsNoTracking()
            .Where(g => g.StartTime >= from)
            .OrderBy(g => g.StartTime)
            .ToListAsync();

        var dieselRecords = await db.DieselRecords
            .AsNoTracking()
            .Where(d => d.RecordDate >= from)
            .OrderBy(d => d.RecordDate)
            .ToListAsync();

        // Generator KPIs
        var outages     = genLogs.Where(g => g.RunReason == GeneratorRunReason.PowerOutage).ToList();
        var totalHours  = genLogs.Where(g => g.RuntimeHours.HasValue).Sum(g => g.RuntimeHours!.Value);
        var totalFuel   = genLogs.Where(g => g.FuelConsumed.HasValue).Sum(g => g.FuelConsumed!.Value);
        var avgDuration = outages.Any(g => g.RuntimeHours.HasValue)
            ? outages.Where(g => g.RuntimeHours.HasValue).Average(g => g.RuntimeHours!.Value)
            : 0;
        var currentlyRunning = await db.GeneratorLogs.CountAsync(g => g.Status == GeneratorLogStatus.Running);

        // Diesel KPIs
        var purchased  = dieselRecords.Where(d => d.RecordType == DieselRecordType.Purchase);
        var dispensed  = dieselRecords.Where(d => d.RecordType == DieselRecordType.Dispensed);
        var totalPurch = purchased.Sum(d => d.QuantityLitres);
        var totalDisp  = dispensed.Sum(d => d.QuantityLitres);
        var totalSpend = purchased.Sum(d => d.TotalCostNaira);

        // All-time stock
        var allPurchased = await db.DieselRecords.AsNoTracking()
            .Where(d => d.RecordType == DieselRecordType.Purchase)
            .SumAsync(d => (double?)d.QuantityLitres) ?? 0;
        var allDispensed = await db.DieselRecords.AsNoTracking()
            .Where(d => d.RecordType == DieselRecordType.Dispensed)
            .SumAsync(d => (double?)d.QuantityLitres) ?? 0;

        // Breakdowns
        var outagesByReason = genLogs
            .GroupBy(g => g.RunReason)
            .Select(g => new PeriodBreakdownItem(g.Key, g.Count()))
            .ToList();

        var dieselByType = dieselRecords
            .GroupBy(d => d.RecordType)
            .Select(g => new PeriodBreakdownItem(g.Key, g.Count()))
            .ToList();

        // Runtime trend (daily)
        var runtimeTrend = genLogs
            .Where(g => g.RuntimeHours.HasValue)
            .GroupBy(g => g.StartTime.Date)
            .OrderBy(g => g.Key)
            .Select(g => new TrendPoint(
                g.Key.ToString("MMM dd"),
                Math.Round(g.Sum(x => x.RuntimeHours!.Value), 2)))
            .ToList();

        // Diesel usage trend (daily dispensed)
        var dieselTrend = dieselRecords
            .Where(d => d.RecordType == DieselRecordType.Dispensed)
            .GroupBy(d => d.RecordDate.Date)
            .OrderBy(g => g.Key)
            .Select(g => new TrendPoint(
                g.Key.ToString("MMM dd"),
                g.Sum(x => x.QuantityLitres)))
            .ToList();

        // Recent generator sessions
        var recentSessions = genLogs
            .OrderByDescending(g => g.StartTime)
            .Take(10)
            .Select(g => new GeneratorSessionItem(
                g.Location, g.RunReason, g.StartTime,
                g.RuntimeHours, g.FuelConsumed, g.OutageCause, g.Status))
            .ToList();

        return Ok(new FuelPowerReportDto(
            Math.Round(totalHours, 2),
            outages.Count,
            Math.Round(totalFuel, 2),
            Math.Round(avgDuration, 2),
            currentlyRunning,
            Math.Round(totalPurch, 2),
            Math.Round(totalDisp, 2),
            totalSpend,
            Math.Round(allPurchased - allDispensed, 2),
            outagesByReason,
            dieselByType,
            runtimeTrend,
            dieselTrend,
            recentSessions,
            label
        ));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  GET /api/v1/reports/vehicle-register
    //  Returns per-vehicle summaries, spares costs, long-standing list,
    //  monthly completion trends, and status×type breakdown for the register.
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("vehicle-register")]
    public async Task<IActionResult> VehicleRegisterReport([FromQuery] string? regNo = null)
    {
        var now  = DateTime.UtcNow;
        var all  = await db.VehicleMaintenanceRequests.AsNoTracking().ToListAsync();

        // Optionally filter to a single vehicle
        var filtered = regNo != null
            ? all.Where(r => r.VehicleRegNo.ToUpper() == regNo.ToUpper().Trim()).ToList()
            : all;

        // ── Per-vehicle register summary ─────────────────────────────────────
        var perVehicle = all
            .GroupBy(r => new { r.VehicleRegNo, r.VehicleType })
            .Select(g => new
            {
                vehicleRegNo        = g.Key.VehicleRegNo,
                vehicleType         = g.Key.VehicleType,
                totalJobs           = g.Count(),
                completedJobs       = g.Count(r => r.Status == VehicleMaintenanceStatus.Completed),
                activeJobs          = g.Count(r => r.Status != VehicleMaintenanceStatus.Completed
                                               && r.Status != VehicleMaintenanceStatus.Rejected),
                totalSparesCost     = g.Where(r => r.SparesCostNaira.HasValue)
                                       .Sum(r => r.SparesCostNaira ?? 0),
                lastServiceDate     = g.Where(r => r.CompletedAt.HasValue)
                                       .OrderByDescending(r => r.CompletedAt)
                                       .Select(r => (DateTime?)r.CompletedAt!.Value)
                                       .FirstOrDefault(),
                currentStatus       = g.OrderByDescending(r => r.CreatedAt)
                                       .Select(r => r.Status.ToString())
                                       .FirstOrDefault(),
            })
            .OrderBy(v => v.vehicleRegNo)
            .ToList();

        // ── Spares cost summary (only vehicles with cost > 0) ────────────────
        var sparesCostSummary = perVehicle
            .Where(v => v.totalSparesCost > 0)
            .OrderByDescending(v => v.totalSparesCost)
            .Select(v => new
            {
                label = v.vehicleRegNo,
                v.vehicleType,
                v.totalSparesCost,
                count = v.totalJobs,
            })
            .ToList();

        // ── Per-vehicle history (for the selected vehicle, or most recent 20) ─
        var history = filtered
            .OrderByDescending(r => r.CreatedAt)
            .Take(regNo != null ? 100 : 20)
            .Select(r => new
            {
                r.RequestNumber,
                r.VehicleRegNo,
                r.VehicleType,
                r.MaintenanceType,
                r.Status,
                r.Priority,
                r.WorkshopName,
                r.WorkshopLocation,
                r.FaultIdentified,
                r.ProposedSolution,
                r.WorkDone,
                r.ActionedBy,
                r.SparesCostNaira,
                r.HandoverConfirmed,
                DaysOpen   = r.Status != VehicleMaintenanceStatus.Completed
                             ? (int)(now - r.CreatedAt).TotalDays : 0,
                r.CreatedAt,
                r.CompletedAt,
            })
            .ToList();

        // ── Long-standing vehicles (InWorkshop > 7 days) ─────────────────────
        var longStanding = all
            .Where(r => (r.Status == VehicleMaintenanceStatus.InWorkshop
                      || r.Status == VehicleMaintenanceStatus.AwaitingParts
                      || r.Status == VehicleMaintenanceStatus.AwaitingFunds)
                      && r.SentToWorkshopAt.HasValue
                      && (now - r.SentToWorkshopAt.Value).TotalDays > 7)
            .OrderByDescending(r => r.SentToWorkshopAt)
            .Select(r => new
            {
                r.RequestNumber,
                r.VehicleRegNo,
                r.VehicleType,
                r.Status,
                r.WorkshopName,
                r.FaultIdentified,
                DaysInWorkshop = (int)(now - r.SentToWorkshopAt!.Value).TotalDays,
                r.SentToWorkshopAt,
            })
            .ToList();

        // ── Monthly completion trends (last 6 months) ────────────────────────
        var monthlyTrends = Enumerable.Range(0, 6)
            .Select(i =>
            {
                var m = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                return new
                {
                    month      = m.ToString("MMM yy"),
                    completed  = all.Count(r => r.Status == VehicleMaintenanceStatus.Completed
                                             && r.CompletedAt.HasValue
                                             && r.CompletedAt.Value.Year  == m.Year
                                             && r.CompletedAt.Value.Month == m.Month),
                    newJobs    = all.Count(r => r.CreatedAt.Year  == m.Year
                                             && r.CreatedAt.Month == m.Month),
                };
            })
            .OrderBy(x => x.month)
            .ToList();

        // ── Status × Type breakdown ──────────────────────────────────────────
        var statusByType = all
            .GroupBy(r => r.MaintenanceType.ToString())
            .Select(g => new
            {
                type          = g.Key,
                pending       = g.Count(r => r.Status == VehicleMaintenanceStatus.Pending),
                inWorkshop    = g.Count(r => r.Status == VehicleMaintenanceStatus.InWorkshop),
                awaitingParts = g.Count(r => r.Status == VehicleMaintenanceStatus.AwaitingParts),
                awaitingFunds = g.Count(r => r.Status == VehicleMaintenanceStatus.AwaitingFunds),
                completed     = g.Count(r => r.Status == VehicleMaintenanceStatus.Completed),
                rejected      = g.Count(r => r.Status == VehicleMaintenanceStatus.Rejected),
            })
            .ToList();

        return Ok(new
        {
            perVehicle,
            sparesCostSummary,
            history,
            longStanding,
            monthlyTrends,
            statusByType,
            totalVehicles        = perVehicle.Count,
            totalSparesCostAll   = all.Where(r => r.SparesCostNaira.HasValue).Sum(r => r.SparesCostNaira ?? 0),
            activeJobsCount      = all.Count(r => r.Status != VehicleMaintenanceStatus.Completed && r.Status != VehicleMaintenanceStatus.Rejected),
            longStandingCount    = longStanding.Count,
        });
    }

    //  GET /api/v1/reports/vehicle?period=30d
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("vehicle")]
    public async Task<IActionResult> VehicleReport([FromQuery] string period = "30d")
    {
        var (from, label) = ResolvePeriod(period);
        var all    = await db.VehicleMaintenanceRequests.AsNoTracking().Where(r => r.CreatedAt >= from).ToListAsync();
        var now    = DateTime.UtcNow;
        var monthS = new DateTime(now.Year, now.Month, 1);

        return Ok(new
        {
            total          = all.Count,
            pending        = all.Count(r => r.Status == VehicleMaintenanceStatus.Pending),
            approved       = all.Count(r => r.Status == VehicleMaintenanceStatus.Approved),
            inWorkshop     = all.Count(r => r.Status == VehicleMaintenanceStatus.InWorkshop),
            completed      = all.Count(r => r.Status == VehicleMaintenanceStatus.Completed),
            rejected       = all.Count(r => r.Status == VehicleMaintenanceStatus.Rejected),
            longStanding   = all.Count(r => r.Status == VehicleMaintenanceStatus.InWorkshop
                                         && r.SentToWorkshopAt.HasValue
                                         && (now - r.SentToWorkshopAt.Value).TotalDays > 7),
            byType         = all.GroupBy(r => r.MaintenanceType)
                                .Select(g => new PeriodBreakdownItem(g.Key, g.Count())).ToList(),
            byLocation     = all.GroupBy(r => r.CurrentLocation)
                                .Select(g => new PeriodBreakdownItem(g.Key, g.Count())).ToList(),
            recentRequests = all.OrderByDescending(r => r.CreatedAt).Take(10)
                                .Select(r => new {
                                    r.RequestNumber, r.VehicleRegNo, r.VehicleType,
                                    r.MaintenanceType, r.Status, r.Priority,
                                    r.CurrentLocation, r.WorkshopName,
                                    DaysOpen = (int)(now - r.CreatedAt).TotalDays,
                                    r.CreatedAt
                                }).ToList(),
            periodLabel    = label,
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  GET /api/v1/reports/facility?period=30d
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("facility")]
    public async Task<IActionResult> FacilityReport([FromQuery] string period = "30d")
    {
        var (from, label) = ResolvePeriod(period);
        var all = await db.FacilityMaintenanceRequests.AsNoTracking().Where(r => r.CreatedAt >= from).ToListAsync();

        return Ok(new
        {
            total            = all.Count,
            pending          = all.Count(r => r.Status == MaintenanceRequestStatus.Pending),
            approved         = all.Count(r => r.Status == MaintenanceRequestStatus.Approved),
            ongoing          = all.Count(r => r.Status == MaintenanceRequestStatus.Ongoing),
            awaitingSpares   = all.Count(r => r.Status == MaintenanceRequestStatus.AwaitingSpares),
            awaitingFunds    = all.Count(r => r.Status == MaintenanceRequestStatus.AwaitingFunds),
            completed        = all.Count(r => r.Status == MaintenanceRequestStatus.Completed),
            byType           = all.GroupBy(r => r.MaintenanceType)
                                  .Select(g => new PeriodBreakdownItem(g.Key, g.Count())).ToList(),
            byLocation       = all.GroupBy(r => r.Location)
                                  .Select(g => new PeriodBreakdownItem(g.Key, g.Count())).ToList(),
            byEndUser        = all.GroupBy(r => r.EndUser)
                                  .Select(g => new PeriodBreakdownItem(g.Key, g.Count())).ToList(),
            recentRequests   = all.OrderByDescending(r => r.CreatedAt).Take(10)
                                  .Select(r => new {
                                      r.RequestNumber, r.MaintenanceType, r.Description,
                                      r.Location, r.EndUser, r.RoomFlat, r.Status, r.Priority,
                                      r.ActionedBy, r.CreatedAt
                                  }).ToList(),
            periodLabel      = label,
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  GET /api/v1/reports/generator?period=30d
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("generator")]
    public async Task<IActionResult> GeneratorReport([FromQuery] string period = "30d")
    {
        var (from, label) = ResolvePeriod(period);
        var readings = await db.GeneratorDailyReadings.AsNoTracking()
            .Where(r => r.ReadingDate >= from).OrderByDescending(r => r.ReadingDate).ToListAsync();

        var allReadings = await db.GeneratorDailyReadings.AsNoTracking()
            .OrderByDescending(r => r.ReadingDate).ToListAsync();

        // Latest reading per generator
        var latestPerGen = allReadings.GroupBy(r => r.AssetNo).Select(g => g.First()).ToList();
        var alertCount   = latestPerGen.Count(r => r.ServiceAlertActive);

        // Total run hours and fuel consumed in period
        var totalHours    = readings.Sum(r => r.RunHoursToday);
        var totalFuel     = readings.Where(r => r.FuelConsumedLitres.HasValue).Sum(r => r.FuelConsumedLitres!.Value);

        var runTrend = readings
            .GroupBy(r => r.ReadingDate.Date)
            .OrderBy(g => g.Key)
            .Select(g => new TrendPoint(g.Key.ToString("MMM dd"), Math.Round(g.Sum(r => r.RunHoursToday), 1)))
            .ToList();

        return Ok(new
        {
            generatorsTracked  = latestPerGen.Count,
            totalRunHoursPeriod= Math.Round(totalHours, 1),
            totalFuelConsumed  = Math.Round(totalFuel, 0),
            serviceAlerts      = alertCount,
            fleetStatus        = latestPerGen.Select(r => new {
                r.AssetNo, r.AssetDescription, r.Location,
                r.CumulativeRunHours, r.FuelLevelLitres,
                r.GeneratorStatus, r.ServiceAlertActive,
                HoursUntilService = r.HoursUntilNextService,
                r.ReadingDate
            }).ToList(),
            runHoursTrend      = runTrend,
            fuelByLocation     = readings.GroupBy(r => r.Location)
                .Select(g => new PeriodBreakdownItem(g.Key, (int)g.Where(r => r.FuelConsumedLitres.HasValue).Sum(r => r.FuelConsumedLitres!.Value)))
                .ToList(),
            periodLabel        = label,
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  GET /api/v1/reports/accommodation?period=30d
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("accommodation")]
    public async Task<IActionResult> AccommodationReport([FromQuery] string period = "30d")
    {
        var (from, label) = ResolvePeriod(period);
        var all = await db.ServiceRequests.AsNoTracking()
            .Where(r => r.Category == RequestCategory.Accommodation && r.CreatedAt >= from)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(new
        {
            total       = all.Count,
            pending     = all.Count(r => r.Status == RequestStatus.PendingLineManager
                                      || r.Status == RequestStatus.PendingApproval),
            approved    = all.Count(r => r.Status == RequestStatus.Approved),
            completed   = all.Count(r => r.Status == RequestStatus.Completed),
            rejected    = all.Count(r => r.Status == RequestStatus.Rejected),
            byLocation  = all.GroupBy(r => r.Location)
                             .Select(g => new PeriodBreakdownItem(g.Key, g.Count())).ToList(),
            requests    = all.Select(r => new {
                r.TicketNumber, r.Title, r.Status, r.Priority,
                r.Location, r.RequestedByName, r.ApprovedByName,
                r.CreatedAt, r.ApprovedAt, r.CompletedAt
            }).ToList(),
            periodLabel = label,
        });
    }

    private static int DaysToExpiry(DateTime expiry) =>
        (int)Math.Ceiling((expiry.Date - DateTime.UtcNow.Date).TotalDays);

    // ══════════════════════════════════════════════════════════════════════════
    //  GET /api/v1/reports/electricity?period=30d   (Obinna — Electricity)
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("electricity")]
    public async Task<IActionResult> ElectricityReport([FromQuery] string period = "30d")
    {
        var (from, label) = ResolvePeriod(period);
        var all = await db.ElectricityPurchases.AsNoTracking().ToListAsync();
        var inPeriod = all.Where(p => p.PurchaseDate >= from).ToList();

        var byLocation = all
            .GroupBy(p => p.Location)
            .Select(g =>
            {
                var latest = g.OrderByDescending(p => p.PurchaseDate).ThenByDescending(p => p.CreatedAt).First();
                return new
                {
                    location    = g.Key,
                    balanceKwh  = latest.BalanceAfterKwh,
                    status      = latest.Status,
                    spendNaira  = g.Where(p => p.PurchaseDate >= from).Sum(p => p.AmountNaira),
                    unitsKwh    = g.Where(p => p.PurchaseDate >= from).Sum(p => p.UnitsKwh),
                };
            })
            .OrderBy(x => x.balanceKwh)
            .ToList();

        return Ok(new
        {
            periodLabel         = label,
            purchaseCount       = inPeriod.Count,
            totalSpendNaira     = inPeriod.Sum(p => p.AmountNaira),
            totalUnitsPurchased = inPeriod.Sum(p => p.UnitsKwh),
            lowBalanceCount     = byLocation.Count(x => x.status != ElectricityStatus.Active),
            byType              = inPeriod.GroupBy(p => p.PurchaseType)
                                          .Select(g => new { label = g.Key, count = g.Count(),
                                                             spend = g.Sum(p => p.AmountNaira),
                                                             units = g.Sum(p => p.UnitsKwh) }).ToList(),
            byLocation,
            recentPurchases     = inPeriod.OrderByDescending(p => p.PurchaseDate).Take(50).Select(p => new {
                p.PurchaseDate, p.PurchaseType, p.Location, p.Vendor,
                p.AmountNaira, p.UnitsKwh, p.BalanceAfterKwh, p.Status, p.LoggedByName
            }).ToList(),
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  GET /api/v1/reports/dstv   (Obinna — DStv subscriptions)
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("dstv")]
    public async Task<IActionResult> DstvReport()
    {
        var all = await db.DstvSubscriptions.AsNoTracking().ToListAsync();

        return Ok(new
        {
            totalSubscriptions = all.Count,
            active             = all.Count(s => DaysToExpiry(s.ExpiryDate) > 7),
            expiringSoon       = all.Count(s => { var d = DaysToExpiry(s.ExpiryDate); return d >= 0 && d <= 7; }),
            expired            = all.Count(s => DaysToExpiry(s.ExpiryDate) < 0),
            totalSpendNaira    = all.Sum(s => s.AmountNaira),
            byLocation         = all.GroupBy(s => s.Location)
                                    .Select(g => new PeriodBreakdownItem(g.Key, g.Count())).ToList(),
            upcoming           = all.Where(s => DaysToExpiry(s.ExpiryDate) <= 30)
                                    .OrderBy(s => s.ExpiryDate)
                                    .Select(s => new {
                                        s.DecoderNumber, s.Location, s.Package, s.ExpiryDate,
                                        daysToExpiry = DaysToExpiry(s.ExpiryDate), s.AmountNaira
                                    }).ToList(),
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  GET /api/v1/reports/vehicle-documents   (Obinna — statutory documents)
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("vehicle-documents")]
    public async Task<IActionResult> VehicleDocumentsReport()
    {
        var docs     = await db.VehicleDocuments.AsNoTracking().ToListAsync();
        var vehicles = await db.Vehicles.AsNoTracking().CountAsync();

        string DocStatus(DateTime e) { var d = DaysToExpiry(e); return d < 0 ? "Expired" : d <= 14 ? "Expiring" : "Valid"; }

        return Ok(new
        {
            totalDocuments   = docs.Count,
            totalVehicles    = vehicles,
            valid            = docs.Count(d => DocStatus(d.ExpiryDate) == "Valid"),
            expiring         = docs.Count(d => DocStatus(d.ExpiryDate) == "Expiring"),
            expired          = docs.Count(d => DocStatus(d.ExpiryDate) == "Expired"),
            renewalSpendNaira= docs.Sum(d => d.RenewalCostNaira ?? 0),
            byType           = docs.GroupBy(d => d.DocumentType)
                                   .Select(g => new PeriodBreakdownItem(g.Key, g.Count())).ToList(),
            expiringNext30   = docs.Where(d => DaysToExpiry(d.ExpiryDate) <= 30)
                                   .OrderBy(d => d.ExpiryDate)
                                   .Select(d => new {
                                       d.VehicleRegNo, d.DocumentType, d.ExpiryDate,
                                       daysToExpiry = DaysToExpiry(d.ExpiryDate),
                                       status = DocStatus(d.ExpiryDate), d.RenewalCostNaira
                                   }).ToList(),
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  GET /api/v1/reports/diesel-supply?period=30d   (Obinna — diesel supply)
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("diesel-supply")]
    public async Task<IActionResult> DieselSupplyReport([FromQuery] string period = "30d")
    {
        var (from, label) = ResolvePeriod(period);
        var supplies = await db.DieselBulkSupplies.AsNoTracking().ToListAsync();
        var dists    = await db.DieselDistributions.AsNoTracking()
                                .Where(d => d.DistributionDate >= from).ToListAsync();

        return Ok(new
        {
            periodLabel             = label,
            totalSuppliedLitres     = supplies.Sum(s => s.QuantityLitres),
            availableBalanceLitres  = supplies.Sum(s => s.QuantityRemainingLitres),
            totalPurchaseValueNaira = supplies.Sum(s => s.TotalCostNaira),
            distributedInPeriod     = dists.Sum(d => d.QuantityLitres),
            distributionByType      = dists.GroupBy(d => d.DistributionType)
                                           .Select(g => new { label = g.Key, count = g.Count(),
                                                              litres = g.Sum(d => d.QuantityLitres) }).ToList(),
            topVehicles             = dists.Where(d => d.VehicleRegNo != null)
                                           .GroupBy(d => d.VehicleRegNo!)
                                           .Select(g => new PeriodBreakdownItem(g.Key, (int)g.Sum(d => d.QuantityLitres)))
                                           .OrderByDescending(x => x.Count).Take(10).ToList(),
            recentDistributions     = dists.OrderByDescending(d => d.DistributionDate).Take(50).Select(d => new {
                d.DistributionReference, d.DistributionDate, d.DistributionType,
                recipient = d.VehicleRegNo ?? d.DestinationLocation,
                d.QuantityLitres, d.BulkSupplyReference, d.IssuingOfficer
            }).ToList(),
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  REPORT EXPLORER — one unified, filterable view over live data
    // ══════════════════════════════════════════════════════════════════════════

    // ── GET /api/v1/reports/explorer ─────────────────────────────────────────
    [HttpGet("explorer")]
    public async Task<IActionResult> Explorer(
        [FromQuery] string  dataset   = "vehicle",
        [FromQuery] string? from      = null,
        [FromQuery] string? to        = null,
        [FromQuery] string? location  = null,
        [FromQuery] string? status    = null,
        [FromQuery] string? type      = null,
        [FromQuery] decimal? minAmount = null,
        [FromQuery] decimal? maxAmount = null,
        [FromQuery] string? search    = null,
        [FromQuery] int     page      = 1,
        [FromQuery] int     pageSize  = 200)
    {
        var (columns, amountKey, rows) = await BuildExplorerAsync(dataset);
        var filtered = FilterExplorer(rows, from, to, location, status, type, minAmount, maxAmount, search);

        var total       = filtered.Count;
        var totalAmount = amountKey is null ? 0m : filtered.Sum(x => x.Amount ?? 0);
        var paged       = filtered.Skip((page - 1) * pageSize).Take(pageSize).Select(x => x.Row).ToList();

        return Ok(new
        {
            dataset,
            columns,
            amountKey,
            rows             = paged,
            totalCount       = total,
            totalAmountNaira = totalAmount,
            page,
            pageSize,
        });
    }

    // ── GET /api/v1/reports/explorer/export ──────────────────────────────────
    [HttpGet("explorer/export")]
    public async Task<IActionResult> ExplorerExport(
        [FromQuery] string  dataset   = "vehicle",
        [FromQuery] string? from      = null,
        [FromQuery] string? to        = null,
        [FromQuery] string? location  = null,
        [FromQuery] string? status    = null,
        [FromQuery] string? type      = null,
        [FromQuery] decimal? minAmount = null,
        [FromQuery] decimal? maxAmount = null,
        [FromQuery] string? search    = null)
    {
        var (columns, amountKey, rows) = await BuildExplorerAsync(dataset);
        var filtered = FilterExplorer(rows, from, to, location, status, type, minAmount, maxAmount, search);

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(dataset.Length > 28 ? dataset[..28] : dataset);

        for (int c = 0; c < columns.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = columns[c].Label;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1677ff");
            cell.Style.Font.FontColor = XLColor.White;
        }

        for (int r = 0; r < filtered.Count; r++)
        {
            var row = filtered[r].Row;
            for (int c = 0; c < columns.Count; c++)
            {
                var col = columns[c];
                var val = row.GetValueOrDefault(col.Key);
                var cell = ws.Cell(r + 2, c + 1);
                if (val is null) { cell.Value = ""; }
                else if (col.Kind == "money" || col.Kind == "number")
                    cell.Value = Convert.ToDouble(val);
                else cell.Value = val.ToString();
            }
            if (r % 2 == 1) ws.Row(r + 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#f5f5f5");
        }

        // Totals row for money columns
        if (amountKey is not null && filtered.Count > 0)
        {
            var totalRow = filtered.Count + 2;
            ws.Cell(totalRow, 1).Value = "TOTAL";
            ws.Cell(totalRow, 1).Style.Font.Bold = true;
            var amtIdx = columns.FindIndex(c => c.Key == amountKey);
            if (amtIdx >= 0)
            {
                ws.Cell(totalRow, amtIdx + 1).Value = (double)filtered.Sum(x => x.Amount ?? 0);
                ws.Cell(totalRow, amtIdx + 1).Style.Font.Bold = true;
            }
        }

        ws.Columns().AdjustToContents(8, 60);
        ws.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var filename = $"Report_{dataset}_{DateTime.UtcNow:yyyyMMdd}.xlsx";
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
    }

    // ── Filter helper (in-memory) ────────────────────────────────────────────
    private static List<ExplorerRowInternal> FilterExplorer(
        List<ExplorerRowInternal> rows, string? from, string? to, string? location,
        string? status, string? type, decimal? minAmount, decimal? maxAmount, string? search)
    {
        DateTime? fromDt = DateTime.TryParse(from, out var fd) ? fd.Date : null;
        DateTime? toDt   = DateTime.TryParse(to,   out var td) ? td.Date.AddDays(1).AddTicks(-1) : null;
        var s = search?.Trim();

        return rows.Where(x =>
            (fromDt is null || (x.Date is not null && x.Date >= fromDt)) &&
            (toDt   is null || (x.Date is not null && x.Date <= toDt)) &&
            (string.IsNullOrWhiteSpace(location) || string.Equals(x.Location, location, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(status)   || string.Equals(x.Status,   status,   StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(type)     || string.Equals(x.Type,     type,     StringComparison.OrdinalIgnoreCase)) &&
            (minAmount is null || (x.Amount is not null && x.Amount >= minAmount)) &&
            (maxAmount is null || (x.Amount is not null && x.Amount <= maxAmount)) &&
            (string.IsNullOrWhiteSpace(s) || x.Search.Contains(s, StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }

    private static ExplorerColumn Col(string key, string label, string kind = "text") => new(key, label, kind);

    // ── Dataset builder ──────────────────────────────────────────────────────
    private async Task<(List<ExplorerColumn> Columns, string? AmountKey, List<ExplorerRowInternal> Rows)>
        BuildExplorerAsync(string dataset)
    {
        switch (dataset)
        {
            case "equipment":
            {
                var cols = new List<ExplorerColumn> {
                    Col("reference","Ref"), Col("assetNo","Asset No."), Col("asset","Asset"),
                    Col("type","Type"), Col("status","Status"), Col("location","Location"),
                    Col("date","Date","date"), Col("amount","Final/Est. Amount","money") };
                var data = await db.EquipmentMaintenanceRequests.AsNoTracking().ToListAsync();
                var rows = data.Select(r => {
                    var amt = r.FinalAmountNaira ?? r.AmountNaira;
                    var row = new Dictionary<string, object?> {
                        ["reference"] = r.RequestNumber, ["assetNo"] = r.AssetNo, ["asset"] = r.AssetDescription,
                        ["type"] = r.MaintenanceType, ["status"] = r.Status, ["location"] = r.Location,
                        ["date"] = r.CreatedAt.ToString("yyyy-MM-dd"), ["amount"] = amt };
                    return new ExplorerRowInternal(row, r.CreatedAt, r.Location, r.Status, r.MaintenanceType, amt,
                        $"{r.RequestNumber} {r.AssetNo} {r.AssetDescription}");
                }).ToList();
                return (cols, "amount", rows);
            }
            case "facility":
            {
                var cols = new List<ExplorerColumn> {
                    Col("reference","Ref"), Col("description","Description"), Col("type","Type"),
                    Col("status","Status"), Col("location","Location"), Col("date","Date","date"),
                    Col("amount","Final/Est. Amount","money") };
                var data = await db.FacilityMaintenanceRequests.AsNoTracking().ToListAsync();
                var rows = data.Select(r => {
                    var amt = r.FinalAmountNaira ?? r.AmountNaira;
                    var row = new Dictionary<string, object?> {
                        ["reference"] = r.RequestNumber, ["description"] = r.Description,
                        ["type"] = r.MaintenanceType, ["status"] = r.Status, ["location"] = r.Location,
                        ["date"] = r.CreatedAt.ToString("yyyy-MM-dd"), ["amount"] = amt };
                    return new ExplorerRowInternal(row, r.CreatedAt, r.Location, r.Status, r.MaintenanceType, amt,
                        $"{r.RequestNumber} {r.Description}");
                }).ToList();
                return (cols, "amount", rows);
            }
            case "diesel":
            {
                var cols = new List<ExplorerColumn> {
                    Col("reference","Ref"), Col("type","Type"), Col("supplyType","Supply Type"),
                    Col("recipient","Recipient"), Col("location","Location"), Col("date","Date","date"),
                    Col("litres","Qty (L)","number"), Col("issuedBy","Issued By") };
                var data = await db.DieselDistributions.AsNoTracking().ToListAsync();
                var rows = data.Select(r => {
                    var recipient = r.VehicleRegNo ?? r.DestinationLocation;
                    var row = new Dictionary<string, object?> {
                        ["reference"] = r.DistributionReference, ["type"] = r.DistributionType,
                        ["supplyType"] = r.SupplyType, ["recipient"] = recipient, ["location"] = r.DestinationLocation,
                        ["date"] = r.DistributionDate.ToString("yyyy-MM-dd"), ["litres"] = r.QuantityLitres,
                        ["issuedBy"] = r.IssuingOfficer };
                    return new ExplorerRowInternal(row, r.DistributionDate, r.DestinationLocation, null, r.DistributionType, null,
                        $"{r.DistributionReference} {recipient} {r.IssuingOfficer}");
                }).ToList();
                return (cols, null, rows);
            }
            case "electricity":
            {
                var cols = new List<ExplorerColumn> {
                    Col("type","Type"), Col("location","Location"), Col("vendor","Vendor"),
                    Col("date","Date","date"), Col("units","Units (kWh)","number"),
                    Col("amount","Amount","money"), Col("status","Status") };
                var data = await db.ElectricityPurchases.AsNoTracking().ToListAsync();
                var rows = data.Select(r => {
                    var row = new Dictionary<string, object?> {
                        ["type"] = r.PurchaseType, ["location"] = r.Location, ["vendor"] = r.Vendor,
                        ["date"] = r.PurchaseDate.ToString("yyyy-MM-dd"), ["units"] = r.UnitsKwh,
                        ["amount"] = r.AmountNaira, ["status"] = r.Status };
                    return new ExplorerRowInternal(row, r.PurchaseDate, r.Location, r.Status, r.PurchaseType, r.AmountNaira,
                        $"{r.Location} {r.Vendor}");
                }).ToList();
                return (cols, "amount", rows);
            }
            case "dstv":
            {
                var cols = new List<ExplorerColumn> {
                    Col("decoder","Decoder"), Col("package","Package"), Col("location","Location"),
                    Col("vendor","Vendor"), Col("start","Start","date"), Col("expiry","Expiry","date"),
                    Col("amount","Amount","money"), Col("status","Status") };
                var data = await db.DstvSubscriptions.AsNoTracking().ToListAsync();
                var rows = data.Select(r => {
                    var row = new Dictionary<string, object?> {
                        ["decoder"] = r.DecoderNumber, ["package"] = r.Package, ["location"] = r.Location,
                        ["vendor"] = r.Vendor, ["start"] = r.StartDate.ToString("yyyy-MM-dd"),
                        ["expiry"] = r.ExpiryDate.ToString("yyyy-MM-dd"), ["amount"] = r.AmountNaira, ["status"] = r.Status };
                    return new ExplorerRowInternal(row, r.StartDate, r.Location, r.Status, r.Package, r.AmountNaira,
                        $"{r.DecoderNumber} {r.Location} {r.Vendor}");
                }).ToList();
                return (cols, "amount", rows);
            }
            case "accommodation":
            {
                var cols = new List<ExplorerColumn> {
                    Col("reference","Ref"), Col("guest","Guest"), Col("location","Guest House"),
                    Col("date","Check-In","date"), Col("nights","Nights","number"), Col("mealPlan","Meal Plan"),
                    Col("status","Status"), Col("amount","Total Cost","money") };
                var data = await db.AccommodationLogs.AsNoTracking().ToListAsync();
                var rows = data.Select(r => {
                    var row = new Dictionary<string, object?> {
                        ["reference"] = r.Reference, ["guest"] = r.GuestName, ["location"] = r.GuestHouse,
                        ["date"] = r.CheckInDate.ToString("yyyy-MM-dd"), ["nights"] = r.Nights, ["mealPlan"] = r.MealPlan,
                        ["status"] = r.Status, ["amount"] = r.TotalCostNaira };
                    return new ExplorerRowInternal(row, r.CheckInDate.ToDateTime(TimeOnly.MinValue), r.GuestHouse, r.Status, r.MealPlan,
                        r.TotalCostNaira, $"{r.Reference} {r.GuestName} {r.Department}");
                }).ToList();
                return (cols, "amount", rows);
            }
            case "requests":
            {
                var cols = new List<ExplorerColumn> {
                    Col("ticket","Ticket"), Col("title","Title"), Col("type","Category"),
                    Col("status","Status"), Col("location","Location"), Col("priority","Priority"),
                    Col("date","Date","date") };
                var data = await db.ServiceRequests.AsNoTracking().ToListAsync();
                var rows = data.Select(r => {
                    var row = new Dictionary<string, object?> {
                        ["ticket"] = r.TicketNumber, ["title"] = r.Title, ["type"] = r.Category,
                        ["status"] = r.Status, ["location"] = r.Location, ["priority"] = r.Priority,
                        ["date"] = r.CreatedAt.ToString("yyyy-MM-dd") };
                    return new ExplorerRowInternal(row, r.CreatedAt, r.Location, r.Status, r.Category, null,
                        $"{r.TicketNumber} {r.Title}");
                }).ToList();
                return (cols, null, rows);
            }
            case "generator":
            {
                var cols = new List<ExplorerColumn> {
                    Col("assetNo","Asset No."), Col("asset","Asset"), Col("location","Location"),
                    Col("date","Date","date"), Col("runHours","Run Hours","number"),
                    Col("fuel","Fuel Used (L)","number"), Col("genKw","Gen kW Used","number"),
                    Col("status","Status") };
                var data = await db.GeneratorDailyReadings.AsNoTracking().ToListAsync();
                var rows = data.Select(r => {
                    var row = new Dictionary<string, object?> {
                        ["assetNo"] = r.AssetNo, ["asset"] = r.AssetDescription, ["location"] = r.Location,
                        ["date"] = r.ReadingDate.ToString("yyyy-MM-dd"), ["runHours"] = r.RunHoursToday,
                        ["fuel"] = r.FuelConsumedLitres, ["genKw"] = r.GeneratorKwConsumed,
                        ["status"] = r.GeneratorStatus };
                    return new ExplorerRowInternal(row, r.ReadingDate, r.Location, r.GeneratorStatus, null, null,
                        $"{r.AssetNo} {r.AssetDescription}");
                }).ToList();
                return (cols, null, rows);
            }
            default: // "vehicle"
            {
                var cols = new List<ExplorerColumn> {
                    Col("reference","Ref"), Col("vehicle","Vehicle"), Col("assetNo","Asset No."),
                    Col("type","Type"), Col("status","Status"), Col("location","Location"),
                    Col("date","Date","date"), Col("amount","Final/Est. Amount","money") };
                var data = await db.VehicleMaintenanceRequests.AsNoTracking().ToListAsync();
                var rows = data.Select(r => {
                    var amt = r.FinalAmountNaira ?? r.AmountNaira;
                    var row = new Dictionary<string, object?> {
                        ["reference"] = r.RequestNumber, ["vehicle"] = r.VehicleRegNo, ["assetNo"] = r.AssetNo,
                        ["type"] = r.MaintenanceType, ["status"] = r.Status, ["location"] = r.CurrentLocation,
                        ["date"] = r.CreatedAt.ToString("yyyy-MM-dd"), ["amount"] = amt };
                    return new ExplorerRowInternal(row, r.CreatedAt, r.CurrentLocation, r.Status, r.MaintenanceType, amt,
                        $"{r.RequestNumber} {r.VehicleRegNo} {r.AssetNo}");
                }).ToList();
                return (cols, "amount", rows);
            }
        }
    }
}
