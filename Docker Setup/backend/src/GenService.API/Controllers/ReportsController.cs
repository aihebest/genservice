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
}
