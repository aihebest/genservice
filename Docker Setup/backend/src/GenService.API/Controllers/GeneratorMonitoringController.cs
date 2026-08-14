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
    private string CallerRole  => User.FindFirst("role")?.Value
                               ?? User.FindFirstValue(ClaimTypes.Role)
                               ?? "Requester";
    /// <summary>Only managers/admins may correct or delete existing records.</summary>
    private bool CanEditRecords => CallerRole is "DepartmentManager" or "SystemAdmin";

    private static GeneratorReadingDto ToDto(GeneratorDailyReading r) => new(
        r.Id, r.AssetNo, r.AssetDescription, r.Location,
        r.ReadingDate,
        r.PreviousEngineReading, r.CurrentEngineReading,
        r.CumulativeRunHours, r.RunHoursToday,
        r.GeneratorStatus,
        r.PreviousFuelLevelLitres, r.FuelLevelLitres,
        r.FuelAddedLitres, r.FuelRemovedLitres, r.FuelConsumedLitres,
        r.PreviousUtilityReading, r.CurrentUtilityReading, r.UtilityAvailableHours,
        r.PreviousGeneratorKw, r.CurrentGeneratorKw, r.GeneratorKwConsumed,
        r.ServiceIntervalHours, r.RemainingServiceHours,
        r.ServiceCompleted, r.LastServicedAtHours,
        r.ServiceAlertActive, r.HoursUntilNextService,
        r.Notes, r.LoggedByEmail, r.LoggedByName, r.CreatedAt,
        r.LastEditedByName, r.LastEditedAt
    );

    // ── GET /api/v1/generator-monitoring/readings ────────────────────────────
    [HttpGet("readings")]
    public async Task<IActionResult> ListReadings([FromQuery] GeneratorReadingQuery q)
    {
        var query = db.GeneratorDailyReadings.AsNoTracking().AsQueryable();

        // Only apply a date window when one is explicitly requested (Days > 0).
        // Days <= 0 means "all history", so a location's readings never disappear
        // from the list just because they were logged more than N days ago.
        if (q.Days > 0)
        {
            var from = DateTime.UtcNow.AddDays(-q.Days).Date;
            query = query.Where(r => r.ReadingDate >= from);
        }

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

        // ── §3 Fuel consumed = (Previous + Added − Removed) − Current ────────────
        // Optional Fuel Added / Removed account for top-ups and withdrawals so the
        // day's true consumption is accurate even when the tank level rises.
        var previousFuel   = req.PreviousFuelLevelLitres
                             ?? last?.FuelLevelLitres
                             ?? req.CurrentFuelLevelLitres;
        var fuelAdded      = req.FuelAddedLitres   ?? 0;
        var fuelRemoved    = req.FuelRemovedLitres ?? 0;
        var fuelDelta      = (previousFuel + fuelAdded - fuelRemoved) - req.CurrentFuelLevelLitres;
        double? fuelConsumed = fuelDelta >= 0 ? fuelDelta : null;   // negative ⇒ likely an unrecorded supply

        // ── §4 Utility (NEPA) available = Current − Previous (hour meter) ───────
        // ── Generator kW consumed = Current kW − Previous kW (BSI audit) ────────
        // Previous auto-fills from the last reading when not supplied, mirroring
        // the engine-hour and utility meters.
        var previousKw     = req.PreviousGeneratorKw ?? last?.CurrentGeneratorKw;
        double? kwConsumed = null;
        if (req.CurrentGeneratorKw.HasValue && previousKw.HasValue)
            kwConsumed = Math.Max(0, req.CurrentGeneratorKw.Value - previousKw.Value);

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
            FuelAddedLitres         = req.FuelAddedLitres,
            FuelRemovedLitres       = req.FuelRemovedLitres,
            FuelConsumedLitres      = fuelConsumed,
            PreviousUtilityReading  = previousUtil,
            CurrentUtilityReading   = req.CurrentUtilityReading,
            UtilityAvailableHours   = utilAvailable,
            PreviousGeneratorKw     = previousKw,
            CurrentGeneratorKw      = req.CurrentGeneratorKw,
            GeneratorKwConsumed     = kwConsumed,
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

    // ── PUT /api/v1/generator-monitoring/readings/{id} ──────────────────────
    /// <summary>
    /// Corrects a previously logged reading. Restricted to Department Managers and
    /// System Admins so data-entry mistakes can be fixed under proper control, and
    /// stamps who edited it and when for audit purposes.
    /// </summary>
    [HttpPut("readings/{id:guid}")]
    public async Task<ActionResult<GeneratorReadingDto>> UpdateReading(
        Guid id, [FromBody] CreateGeneratorReadingRequest req)
    {
        if (!CanEditRecords)
            return StatusCode(403, new { message = "Only a Department Manager or System Admin can edit existing records." });

        var r = await db.GeneratorDailyReadings.FindAsync(id);
        if (r is null) return NotFound();

        var interval = req.ServiceIntervalHours > 0 ? req.ServiceIntervalHours : 250;

        r.AssetNo          = req.AssetNo.Trim();
        r.AssetDescription = req.AssetDescription.Trim();
        r.Location         = req.Location.Trim();
        if (req.ReadingDate.HasValue) r.ReadingDate = req.ReadingDate.Value.Date;

        // Engine hours
        r.PreviousEngineReading = req.PreviousEngineReading ?? r.PreviousEngineReading;
        r.CurrentEngineReading  = req.CurrentEngineReading;
        r.RunHoursToday         = Math.Max(0, r.CurrentEngineReading - r.PreviousEngineReading);
        r.CumulativeRunHours    = r.CurrentEngineReading;
        r.GeneratorStatus       = req.GeneratorStatus;

        // Fuel
        r.PreviousFuelLevelLitres = req.PreviousFuelLevelLitres ?? r.PreviousFuelLevelLitres;
        r.FuelLevelLitres         = req.CurrentFuelLevelLitres;
        r.FuelAddedLitres         = req.FuelAddedLitres;
        r.FuelRemovedLitres       = req.FuelRemovedLitres;
        var delta = (r.PreviousFuelLevelLitres + (req.FuelAddedLitres ?? 0) - (req.FuelRemovedLitres ?? 0))
                    - req.CurrentFuelLevelLitres;
        r.FuelConsumedLitres = delta >= 0 ? delta : null;

        // Utility + generator kW meters
        r.PreviousUtilityReading = req.PreviousUtilityReading ?? r.PreviousUtilityReading;
        r.CurrentUtilityReading  = req.CurrentUtilityReading;
        r.UtilityAvailableHours  = (req.CurrentUtilityReading.HasValue && r.PreviousUtilityReading.HasValue)
            ? Math.Max(0, req.CurrentUtilityReading.Value - r.PreviousUtilityReading.Value) : null;

        r.PreviousGeneratorKw = req.PreviousGeneratorKw ?? r.PreviousGeneratorKw;
        r.CurrentGeneratorKw  = req.CurrentGeneratorKw;
        r.GeneratorKwConsumed = (req.CurrentGeneratorKw.HasValue && r.PreviousGeneratorKw.HasValue)
            ? Math.Max(0, req.CurrentGeneratorKw.Value - r.PreviousGeneratorKw.Value) : null;

        // Service countdown
        r.ServiceIntervalHours = interval;
        r.ServiceCompleted     = req.ServiceCompleted;
        if (req.ServiceCompleted)
        {
            r.RemainingServiceHours = interval;
            r.LastServicedAtHours   = req.CurrentEngineReading;
        }
        else if (req.LastServicedAtHours.HasValue)
        {
            r.LastServicedAtHours = req.LastServicedAtHours;
        }
        r.ServiceAlertActive = !req.ServiceCompleted &&
            r.RemainingServiceHours <= GeneratorDailyReading.ServiceAlertThresholdHours;

        r.Notes            = req.Notes?.Trim() ?? r.Notes;
        r.UpdatedAt        = DateTime.UtcNow;
        r.LastEditedByName = CallerName;
        r.LastEditedAt     = DateTime.UtcNow;

        await db.SaveChangesAsync();
        logger.LogInformation("Generator reading {Id} edited by {User}", id, CallerEmail);
        return Ok(ToDto(r));
    }

    // ── DELETE /api/v1/generator-monitoring/readings/{id} ───────────────────
    [HttpDelete("readings/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!CanEditRecords)
            return StatusCode(403, new { message = "Only a Department Manager or System Admin can delete records." });
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
    private string CallerRole  => User.FindFirst("role")?.Value
                               ?? User.FindFirstValue(ClaimTypes.Role)
                               ?? "Requester";
    /// <summary>Only managers/admins may correct or delete existing records.</summary>
    private bool CanEditRecords => CallerRole is "DepartmentManager" or "SystemAdmin";

    /// <summary>Fixed NPA electricity tariff (₦ per kWh).</summary>
    private const decimal ElectricityRateNaira = 209m;

    private static PowerMeterReadingDto ToDto(PowerMeterReading r) => new(
        r.Id, r.Location, r.MeterNumber, r.ReadingDate,
        r.PreviousMeterReading, r.CurrentMeterReading,
        r.MeterReadingKwh, r.UnitsConsumedToday, r.UtilityAvailableHours,
        r.CostPerKwhNaira, r.TotalElectricityCostNaira,
        r.Notes, r.LoggedByEmail, r.LoggedByName, r.CreatedAt,
        r.LastEditedByName, r.LastEditedAt
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

    // ── PUT /api/v1/power-meter/{id} ────────────────────────────────────────
    /// <summary>Manager-only correction of a power meter reading, with edit audit stamp.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PowerMeterReadingDto>> Update(
        Guid id, [FromBody] CreatePowerMeterReadingRequest req)
    {
        if (!CanEditRecords)
            return StatusCode(403, new { message = "Only a Department Manager or System Admin can edit existing records." });

        var r = await db.PowerMeterReadings.FindAsync(id);
        if (r is null) return NotFound();

        var consumed  = Math.Max(0, req.CurrentMeterReading - req.PreviousMeterReading);
        var totalCost = (decimal)consumed * ElectricityRateNaira;

        r.Location                  = req.Location.Trim();
        r.MeterNumber               = req.MeterNumber.Trim();
        if (req.ReadingDate.HasValue) r.ReadingDate = req.ReadingDate.Value.Date;
        r.PreviousMeterReading      = req.PreviousMeterReading;
        r.CurrentMeterReading       = req.CurrentMeterReading;
        r.MeterReadingKwh           = req.CurrentMeterReading;
        r.UnitsConsumedToday        = consumed;
        r.UtilityAvailableHours     = req.UtilityAvailableHours;
        r.CostPerKwhNaira           = ElectricityRateNaira;
        r.TotalElectricityCostNaira = totalCost;
        r.Notes                     = req.Notes?.Trim() ?? r.Notes;
        r.LastEditedByName          = CallerName;
        r.LastEditedAt              = DateTime.UtcNow;

        await db.SaveChangesAsync();
        logger.LogInformation("Power meter reading {Id} edited by {User}", id, CallerEmail);
        return Ok(ToDto(r));
    }

    // ── DELETE /api/v1/power-meter/{id} ─────────────────────────────────────
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!CanEditRecords)
            return StatusCode(403, new { message = "Only a Department Manager or System Admin can delete records." });
        var r = await db.PowerMeterReadings.FindAsync(id);
        if (r is null) return NotFound();
        db.PowerMeterReadings.Remove(r);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
