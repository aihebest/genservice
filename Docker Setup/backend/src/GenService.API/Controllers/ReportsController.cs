using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenService.API.Controllers;

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
}
