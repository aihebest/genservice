using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
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
    ILogger<GeneratorMonitoringController> logger) : ControllerBase
{
    private string CallerEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";
    private string CallerName  => User.FindFirstValue(ClaimTypes.Name)  ?? "";

    private static GeneratorReadingDto ToDto(GeneratorDailyReading r) => new(
        r.Id, r.AssetNo, r.AssetDescription, r.Location,
        r.ReadingDate, r.CumulativeRunHours, r.RunHoursToday,
        r.GeneratorStatus, r.FuelLevelLitres, r.FuelConsumedLitres,
        r.UtilityAvailableHours,
        r.ServiceIntervalHours, r.LastServicedAtHours,
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
        // Service alert: trigger if within 20 hours of next service
        var hoursSinceLast  = req.LastServicedAtHours.HasValue
            ? req.CumulativeRunHours - req.LastServicedAtHours.Value
            : req.CumulativeRunHours;
        var hoursUntilNext  = req.ServiceIntervalHours - (hoursSinceLast % req.ServiceIntervalHours);
        var alertActive     = hoursUntilNext <= 20;

        var reading = new GeneratorDailyReading
        {
            AssetNo              = req.AssetNo.Trim(),
            AssetDescription     = req.AssetDescription.Trim(),
            Location             = req.Location.Trim(),
            ReadingDate          = DateTime.UtcNow.Date,
            CumulativeRunHours   = req.CumulativeRunHours,
            RunHoursToday        = req.RunHoursToday,
            GeneratorStatus      = req.GeneratorStatus,
            FuelLevelLitres      = req.FuelLevelLitres,
            FuelConsumedLitres   = req.FuelConsumedLitres,
            UtilityAvailableHours= req.UtilityAvailableHours,
            ServiceIntervalHours = req.ServiceIntervalHours,
            LastServicedAtHours  = req.LastServicedAtHours,
            ServiceAlertActive   = alertActive,
            Notes                = req.Notes?.Trim(),
            LoggedByEmail        = CallerEmail,
            LoggedByName         = CallerName,
            CreatedAt            = DateTime.UtcNow,
            UpdatedAt            = DateTime.UtcNow,
        };

        db.GeneratorDailyReadings.Add(reading);
        await db.SaveChangesAsync();

        if (alertActive)
            logger.LogWarning("⚠️ SERVICE ALERT: {Asset} at {Location} — only {Hours:0.0}h until next service",
                req.AssetDescription, req.Location, hoursUntilNext);
        else
            logger.LogInformation("Generator reading logged: {Asset} {Location} — {Hours}h cumulative",
                req.AssetNo, req.Location, req.CumulativeRunHours);

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

    private static PowerMeterReadingDto ToDto(PowerMeterReading r) => new(
        r.Id, r.Location, r.MeterNumber, r.ReadingDate,
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
        // Auto-calculate units consumed by comparing with previous reading for this location
        var prev = await db.PowerMeterReadings
            .Where(r => r.Location == req.Location && r.MeterNumber == req.MeterNumber)
            .OrderByDescending(r => r.ReadingDate)
            .FirstOrDefaultAsync();

        double? consumed = prev != null
            ? req.MeterReadingKwh - prev.MeterReadingKwh
            : null;

        // Auto-calculate electricity cost if rate is provided
        decimal? totalCost = null;
        if (req.CostPerKwhNaira.HasValue && consumed > 0)
            totalCost = req.CostPerKwhNaira.Value * (decimal)consumed.Value;

        var reading = new PowerMeterReading
        {
            Location                 = req.Location.Trim(),
            MeterNumber              = req.MeterNumber.Trim(),
            ReadingDate              = DateTime.UtcNow.Date,
            MeterReadingKwh          = req.MeterReadingKwh,
            UnitsConsumedToday       = consumed > 0 ? consumed : null,
            UtilityAvailableHours    = req.UtilityAvailableHours,
            CostPerKwhNaira          = req.CostPerKwhNaira,
            TotalElectricityCostNaira= totalCost,
            Notes                    = req.Notes?.Trim(),
            LoggedByEmail            = CallerEmail,
            LoggedByName             = CallerName,
            CreatedAt                = DateTime.UtcNow,
        };

        db.PowerMeterReadings.Add(reading);
        await db.SaveChangesAsync();

        logger.LogInformation("Power meter reading: {Location} {Meter} — {Reading} kWh",
            req.Location, req.MeterNumber, req.MeterReadingKwh);

        return CreatedAtAction(nameof(List), ToDto(reading));
    }
}
