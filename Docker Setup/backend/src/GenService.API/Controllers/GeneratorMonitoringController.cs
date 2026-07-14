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
[Route("api/v1/generator-monitoring")]
[Authorize]
public class GeneratorMonitoringController(
    GenServiceDbContext db,
    NotificationService notify,
    ILogger<GeneratorMonitoringController> logger) : ControllerBase
{
    private string CallerEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";
    private string CallerName  => User.FindFirstValue(ClaimTypes.Name)  ?? "";

    private static GeneratorReadingDto ToDto(GeneratorDailyReading r) => new(
        r.Id, r.AssetNo, r.AssetDescription, r.Location,
        r.ReadingDate,
        r.PreviousEngineReading, r.CurrentEngineReading,
        r.CumulativeRunHours, r.RunHoursToday,
        r.GeneratorStatus,
        r.PreviousFuelLevelLitres, r.FuelLevelLitres, r.FuelConsumedLitres,
        r.PreviousUtilityReading, r.CurrentUtilityReading, r.UtilityAvailableHours,
        r.ServiceIntervalHours, r.RemainingServiceHours,
        r.ServiceCompleted, r.LastServicedAtHours,
        r.ServiceAlertActive, r.HoursUntilNextService,
        r.Notes, r.LoggedByEmail, r.LoggedByName, r.CreatedAt
    );

    // ── GET /api/v1/generator-monitoring/readings ────────────────────────────
    [HttpGet("readings")]
    public async Task<IActionResult> ListReadings([FromQuery] GeneratorReadingQuery q)
    {
        var from  = DateTime.UtcNow.AddDays(-q.Days).Date;
        var query = db.GeneratorDailyReadings.AsNoTracking()
                      .Where(r => r.ReadingDate >= from);

        if (!string.IsNullOrWhiteSpace(q.Location))
            query = query.Where(r => r.Location == q.Location);
        if (!string.IsNullOrWhiteSpace(q.AssetNo))
            query = query.Where(r => r.AssetNo == q.AssetNo);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.ReadingDate)
            .ThenByDescending(r => r.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return Ok(new { items = items.Select(ToDto), totalCount = total, page = q.Page, pageSize = q.PageSize });
    }

    // ── GET /api/v1/generator-monitoring/summary ─────────────────────────────
    /// <summary>Latest reading per generator for the fleet overview.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<IEnumerable<GeneratorSummaryByLocation>>> Summary()
    {
        var all = await db.GeneratorDailyReadings
            .AsNoTracking()
            .OrderByDescending(r => r.ReadingDate)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync();

        // Take only the latest reading per AssetNo
        var latest = all
            .GroupBy(r => r.AssetNo)
            .Select(g => g.First())
            .Select(r => new GeneratorSummaryByLocation(
                r.Location, r.AssetNo, r.AssetDescription,
                r.CumulativeRunHours, r.HoursUntilNextService,
                r.ServiceAlertActive, r.FuelLevelLitres,
                r.GeneratorStatus, r.ReadingDate))
            .OrderBy(s => s.HoursUntilNextService)
            .ToList();

        return Ok(latest);
    }

    // ── GET /api/v1/generator-monitoring/alerts ──────────────────────────────
    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts()
    {
        var alerts = await db.GeneratorDailyReadings
            .AsNoTracking()
            .Where(r => r.ServiceAlertActive)
            .OrderByDescending(r => r.ReadingDate)
            .Take(20)
            .ToListAsync();

        return Ok(alerts.Select(ToDto));
    }

    // ── POST /api/v1/generator-monitoring/readings ───────────────────────────
    [HttpPost("readings")]
    public async Task<ActionResult<GeneratorReadingDto>> Create(
        [FromBody] CreateGeneratorReadingRequest req)
    {
        var interval = req.ServiceIntervalHours > 0 ? req.ServiceIntervalHours : 250;

        // Pull the last reading for this generator to auto-fill previous values &
        // carry forward the remaining-service countdown.
        var last = await db.GeneratorDailyReadings.AsNoTracking()
            .Where(r => r.AssetNo == req.AssetNo.Trim())
            .OrderByDescending(r => r.ReadingDate)
            .ThenByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        // ── §1 Run hours (24h) = Current − Previous ─────────────────────────────
        var previousEngine = req.PreviousEngineReading
                             ?? last?.CurrentEngineReading
                             ?? req.CurrentEngineReading;
        var runHoursToday  = Math.Max(0, req.CurrentEngineReading - previousEngine);

        // ── §3 Fuel consumed = Previous − Current ───────────────────────────────
        var previousFuel   = req.PreviousFuelLevelLitres
                             ?? last?.FuelLevelLitres
                             ?? req.CurrentFuelLevelLitres;
        var fuelDelta      = previousFuel - req.CurrentFuelLevelLitres;
        double? fuelConsumed = fuelDelta >= 0 ? fuelDelta : null;   // negative ⇒ refill, not consumption

        // ── §4 Utility (NEPA) available = Current − Previous (hour meter) ───────
        var previousUtil   = req.PreviousUtilityReading ?? last?.CurrentUtilityReading;
        double? utilAvailable = null;
        if (req.CurrentUtilityReading.HasValue && previousUtil.HasValue)
            utilAvailable = Math.Max(0, req.CurrentUtilityReading.Value - previousUtil.Value);

        // ── §2 Remaining service hours countdown ───────────────────────────────
        // Deduct today's run hours from the carried-forward remaining balance.
        // Reset to a full interval when a service is recorded.
        var prevRemaining  = last?.RemainingServiceHours ?? interval;
        double remaining;
        double? lastServicedAt;
        if (req.ServiceCompleted)
        {
            remaining      = interval;
            lastServicedAt = req.CurrentEngineReading;
        }
        else
        {
            remaining      = Math.Max(0, prevRemaining - runHoursToday);
            lastServicedAt = req.LastServicedAtHours ?? last?.LastServicedAtHours;
        }

        // Alert when the generator is at/under the 150-hour service threshold.
        var alertActive = !req.ServiceCompleted &&
                          remaining <= GeneratorDailyReading.ServiceAlertThresholdHours;

        var reading = new GeneratorDailyReading
        {
            AssetNo                 = req.AssetNo.Trim(),
            AssetDescription        = req.AssetDescription.Trim(),
            Location                = req.Location.Trim(),
            ReadingDate             = req.ReadingDate?.Date ?? DateTime.UtcNow.Date,
            PreviousEngineReading   = previousEngine,
            CurrentEngineReading    = req.CurrentEngineReading,
            RunHoursToday           = runHoursToday,
            CumulativeRunHours      = req.CurrentEngineReading,
            GeneratorStatus         = req.GeneratorStatus,
            PreviousFuelLevelLitres = previousFuel,
            FuelLevelLitres         = req.CurrentFuelLevelLitres,
            FuelConsumedLitres      = fuelConsumed,
            PreviousUtilityReading  = previousUtil,
            CurrentUtilityReading   = req.CurrentUtilityReading,
            UtilityAvailableHours   = utilAvailable,
            ServiceIntervalHours    = interval,
            RemainingServiceHours   = remaining,
            ServiceCompleted        = req.ServiceCompleted,
            LastServicedAtHours     = lastServicedAt,
            ServiceAlertActive      = alertActive,
            Notes                   = req.Notes?.Trim(),
            LoggedByEmail           = CallerEmail,
            LoggedByName            = CallerName,
            CreatedAt               = DateTime.UtcNow,
            UpdatedAt               = DateTime.UtcNow,
        };

        db.GeneratorDailyReadings.Add(reading);
        await db.SaveChangesAsync();

        // Raise an in-app maintenance notification when approaching the service interval.
        if (alertActive)
        {
            await notify.CreateAsync(
                title:      $"⚠️ Generator service due soon: {reading.AssetDescription}",
                message:    $"{reading.AssetDescription} ({reading.AssetNo}) at {reading.Location} has "
                          + $"{remaining:0} h remaining until its next {interval:0}-hour service.",
                type:       NotificationType.MaintenanceDueSoon,
                module:     "Generator",
                entityId:   reading.Id.ToString(),
                refNumber:  reading.AssetNo,
                targetRole: NotificationTarget.Management);

            logger.LogWarning("⚠️ SERVICE ALERT: {Asset} at {Location} — only {Hours:0.0}h remaining until service",
                reading.AssetDescription, reading.Location, remaining);
        }
        else if (req.ServiceCompleted)
        {
            logger.LogInformation("🔧 Service recorded: {Asset} {Location} — countdown reset to {Interval}h",
                reading.AssetNo, reading.Location, interval);
        }
        else
        {
            logger.LogInformation("Generator reading logged: {Asset} {Location} — ran {Run}h today, {Rem}h to service",
                reading.AssetNo, reading.Location, runHoursToday, remaining);
        }

        return CreatedAtAction(nameof(Summary), ToDto(reading));
    }

    // ── DELETE /api/v1/generator-monitoring/readings/{id} ───────────────────
    [HttpDelete("readings/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var r = await db.GeneratorDailyReadings.FindAsync(id);
        if (r is null) return NotFound();
        db.GeneratorDailyReadings.Remove(r);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

// ── Power Meter Controller ────────────────────────────────────────────────────

[ApiController]
[Route("api/v1/power-meter")]
[Authorize]
public class PowerMeterController(
    GenServiceDbContext db,
    ILogger<PowerMeterController> logger) : ControllerBase
{
    private string CallerEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";
    private string CallerName  => User.FindFirstValue(ClaimTypes.Name)  ?? "";

    /// <summary>Fixed NPA electricity tariff (₦ per kWh).</summary>
    private const decimal ElectricityRateNaira = 209m;

    private static PowerMeterReadingDto ToDto(PowerMeterReading r) => new(
        r.Id, r.Location, r.MeterNumber, r.ReadingDate,
        r.PreviousMeterReading, r.CurrentMeterReading,
        r.MeterReadingKwh, r.UnitsConsumedToday, r.UtilityAvailableHours,
        r.CostPerKwhNaira, r.TotalElectricityCostNaira,
        r.Notes, r.LoggedByEmail, r.LoggedByName, r.CreatedAt
    );

    // ── GET /api/v1/power-meter ──────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PowerMeterQuery q)
    {
        var from  = DateTime.UtcNow.AddDays(-q.Days).Date;
        var query = db.PowerMeterReadings.AsNoTracking()
                      .Where(r => r.ReadingDate >= from);

        if (!string.IsNullOrWhiteSpace(q.Location))
            query = query.Where(r => r.Location == q.Location);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.ReadingDate)
            .ThenByDescending(r => r.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return Ok(new { items = items.Select(ToDto), totalCount = total });
    }

    // ── POST /api/v1/power-meter ─────────────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<PowerMeterReadingDto>> Create(
        [FromBody] CreatePowerMeterReadingRequest req)
    {
        // Consumed (24h) = Current − Previous (user-entered readings)
        var consumed = Math.Max(0, req.CurrentMeterReading - req.PreviousMeterReading);

        // Total cost = consumed × fixed ₦209/kWh tariff
        var totalCost = ElectricityRateNaira * (decimal)consumed;

        var reading = new PowerMeterReading
        {
            Location                 = req.Location.Trim(),
            MeterNumber              = req.MeterNumber.Trim(),
            ReadingDate              = req.ReadingDate?.Date ?? DateTime.UtcNow.Date,
            PreviousMeterReading     = req.PreviousMeterReading,
            CurrentMeterReading      = req.CurrentMeterReading,
            MeterReadingKwh          = req.CurrentMeterReading,
            UnitsConsumedToday       = consumed,
            UtilityAvailableHours    = req.UtilityAvailableHours,
            CostPerKwhNaira          = ElectricityRateNaira,
            TotalElectricityCostNaira= totalCost,
            Notes                    = req.Notes?.Trim(),
            LoggedByEmail            = CallerEmail,
            LoggedByName             = CallerName,
            CreatedAt                = DateTime.UtcNow,
        };

        db.PowerMeterReadings.Add(reading);
        await db.SaveChangesAsync();

        logger.LogInformation("Power meter reading: {Location} {Meter} — {Consumed} kWh consumed, ₦{Cost}",
            req.Location, req.MeterNumber, consumed, totalCost);

        return CreatedAtAction(nameof(List), ToDto(reading));
    }
}
