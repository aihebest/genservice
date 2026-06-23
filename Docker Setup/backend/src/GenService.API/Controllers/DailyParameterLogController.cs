using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/daily-parameter-log")]
[Authorize]
public class DailyParameterLogController(
    GenServiceDbContext db,
    ILogger<DailyParameterLogController> logger) : ControllerBase
{
    private string CallerEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";
    private string CallerName  => User.FindFirstValue(ClaimTypes.Name)  ?? "";

    private static DailyParameterLogDto ToDto(DailyParameterLog r) => new(
        r.Id,
        r.LogDate.ToString("yyyy-MM-dd"),
        r.Location,
        r.NepaHoursAvailable,
        r.GeneratorHoursRun,
        r.DieselConsumedLitres,
        r.DieselBalanceLitres,
        r.GeneratorStatus,
        r.GeneratorRunHourMeter,
        r.WaterSource,
        r.WaterTankLevelPercent,
        r.WaterStatus,
        r.StaffPresent,
        r.ExpectedStaff,
        r.VisitorCount,
        r.CleaningDone,
        r.WasteDisposed,
        r.SecurityStatus,
        r.MaintenanceIssues,
        r.ActionsTaken,
        r.PendingActions,
        r.GeneralRemarks,
        r.LoggedByEmail,
        r.LoggedByName,
        r.CreatedAt,
        r.UpdatedAt
    );

    // ── GET /api/v1/daily-parameter-log ─────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<DailyParameterLogListResponse>> List(
        [FromQuery] DailyParameterLogQuery q)
    {
        var query = db.DailyParameterLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Location))
            query = query.Where(r => r.Location == q.Location);

        if (!string.IsNullOrWhiteSpace(q.From) && DateOnly.TryParse(q.From, out var from))
            query = query.Where(r => r.LogDate >= from);

        if (!string.IsNullOrWhiteSpace(q.To) && DateOnly.TryParse(q.To, out var to))
            query = query.Where(r => r.LogDate <= to);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.LogDate)
            .ThenBy(r => r.Location)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return Ok(new DailyParameterLogListResponse(items.Select(ToDto), total, q.Page, q.PageSize));
    }

    // ── GET /api/v1/daily-parameter-log/stats ───────────────────────────────
    [HttpGet("stats")]
    public async Task<ActionResult<DailyParameterLogStatsDto>> Stats()
    {
        var now        = DateTime.UtcNow;
        var monthStart = new DateOnly(now.Year, now.Month, 1);
        var all        = await db.DailyParameterLogs
            .Where(r => r.LogDate >= monthStart)
            .ToListAsync();

        return Ok(new DailyParameterLogStatsDto(
            LogsThisMonth:               all.Count,
            AvgNepaHoursThisMonth:       all.Any(r => r.NepaHoursAvailable.HasValue)
                ? all.Where(r => r.NepaHoursAvailable.HasValue).Average(r => r.NepaHoursAvailable!.Value)
                : null,
            AvgGeneratorHoursThisMonth:  all.Any(r => r.GeneratorHoursRun.HasValue)
                ? all.Where(r => r.GeneratorHoursRun.HasValue).Average(r => r.GeneratorHoursRun!.Value)
                : null,
            TotalDieselThisMonth:        all.Any(r => r.DieselConsumedLitres.HasValue)
                ? all.Where(r => r.DieselConsumedLitres.HasValue).Sum(r => r.DieselConsumedLitres!.Value)
                : null,
            LocationsLogged:             all.Select(r => r.Location).Distinct().Count()
        ));
    }

    // ── GET /api/v1/daily-parameter-log/{id} ────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DailyParameterLogDto>> GetById(Guid id)
    {
        var r = await db.DailyParameterLogs.FindAsync(id);
        if (r is null) return NotFound();
        return Ok(ToDto(r));
    }

    // ── GET /api/v1/daily-parameter-log/today/{location} ────────────────────
    [HttpGet("today/{location}")]
    public async Task<ActionResult<DailyParameterLogDto?>> GetToday(string location)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var r = await db.DailyParameterLogs
            .FirstOrDefaultAsync(x => x.Location == location && x.LogDate == today);
        if (r is null) return Ok(null);
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/daily-parameter-log ────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<DailyParameterLogDto>> Create(
        [FromBody] CreateDailyParameterLogRequest req)
    {
        if (!DateOnly.TryParse(req.LogDate, out var logDate))
            return BadRequest(new { message = "Invalid LogDate format. Use YYYY-MM-DD." });

        // Prevent duplicate (location + date)
        var exists = await db.DailyParameterLogs.AnyAsync(
            x => x.Location == req.Location.Trim() && x.LogDate == logDate);
        if (exists)
            return Conflict(new { message = $"A log for {req.Location} on {req.LogDate} already exists. Use PUT to update it." });

        var r = new DailyParameterLog
        {
            LogDate               = logDate,
            Location              = req.Location.Trim(),
            NepaHoursAvailable    = req.NepaHoursAvailable,
            GeneratorHoursRun     = req.GeneratorHoursRun,
            DieselConsumedLitres  = req.DieselConsumedLitres,
            DieselBalanceLitres   = req.DieselBalanceLitres,
            GeneratorStatus       = req.GeneratorStatus,
            GeneratorRunHourMeter = req.GeneratorRunHourMeter,
            WaterSource           = req.WaterSource,
            WaterTankLevelPercent = req.WaterTankLevelPercent,
            WaterStatus           = req.WaterStatus,
            StaffPresent          = req.StaffPresent,
            ExpectedStaff         = req.ExpectedStaff,
            VisitorCount          = req.VisitorCount,
            CleaningDone          = req.CleaningDone,
            WasteDisposed         = req.WasteDisposed,
            SecurityStatus        = req.SecurityStatus,
            MaintenanceIssues     = req.MaintenanceIssues?.Trim(),
            ActionsTaken          = req.ActionsTaken?.Trim(),
            PendingActions        = req.PendingActions?.Trim(),
            GeneralRemarks        = req.GeneralRemarks?.Trim(),
            LoggedByEmail         = CallerEmail,
            LoggedByName          = CallerName,
            CreatedAt             = DateTime.UtcNow,
            UpdatedAt             = DateTime.UtcNow,
        };

        db.DailyParameterLogs.Add(r);
        await db.SaveChangesAsync();

        logger.LogInformation("Daily parameter log created: {Location} {Date} by {User}",
            r.Location, r.LogDate, CallerEmail);

        return CreatedAtAction(nameof(GetById), new { id = r.Id }, ToDto(r));
    }

    // ── PUT /api/v1/daily-parameter-log/{id} ────────────────────────────────
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DailyParameterLogDto>> Update(
        Guid id, [FromBody] UpdateDailyParameterLogRequest req)
    {
        var r = await db.DailyParameterLogs.FindAsync(id);
        if (r is null) return NotFound();

        if (req.NepaHoursAvailable.HasValue)    r.NepaHoursAvailable    = req.NepaHoursAvailable;
        if (req.GeneratorHoursRun.HasValue)      r.GeneratorHoursRun     = req.GeneratorHoursRun;
        if (req.DieselConsumedLitres.HasValue)   r.DieselConsumedLitres  = req.DieselConsumedLitres;
        if (req.DieselBalanceLitres.HasValue)    r.DieselBalanceLitres   = req.DieselBalanceLitres;
        if (req.GeneratorStatus     != null)     r.GeneratorStatus       = req.GeneratorStatus;
        if (req.GeneratorRunHourMeter.HasValue)  r.GeneratorRunHourMeter = req.GeneratorRunHourMeter;
        if (req.WaterSource         != null)     r.WaterSource           = req.WaterSource;
        if (req.WaterTankLevelPercent.HasValue)  r.WaterTankLevelPercent = req.WaterTankLevelPercent;
        if (req.WaterStatus         != null)     r.WaterStatus           = req.WaterStatus;
        if (req.StaffPresent.HasValue)           r.StaffPresent          = req.StaffPresent;
        if (req.ExpectedStaff.HasValue)          r.ExpectedStaff         = req.ExpectedStaff;
        if (req.VisitorCount.HasValue)           r.VisitorCount          = req.VisitorCount;
        if (req.CleaningDone.HasValue)           r.CleaningDone          = req.CleaningDone.Value;
        if (req.WasteDisposed.HasValue)          r.WasteDisposed         = req.WasteDisposed.Value;
        if (req.SecurityStatus      != null)     r.SecurityStatus        = req.SecurityStatus;
        if (req.MaintenanceIssues   != null)     r.MaintenanceIssues     = req.MaintenanceIssues.Trim();
        if (req.ActionsTaken        != null)     r.ActionsTaken          = req.ActionsTaken.Trim();
        if (req.PendingActions      != null)     r.PendingActions        = req.PendingActions.Trim();
        if (req.GeneralRemarks      != null)     r.GeneralRemarks        = req.GeneralRemarks.Trim();

        r.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ToDto(r));
    }

    // ── DELETE /api/v1/daily-parameter-log/{id} ─────────────────────────────
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var r = await db.DailyParameterLogs.FindAsync(id);
        if (r is null) return NotFound();
        db.DailyParameterLogs.Remove(r);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
