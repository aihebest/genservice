using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/fuel")]
[Authorize]
public class FuelController(GenServiceDbContext db) : ControllerBase
{
    // ── Auth helpers ──────────────────────────────────────────────────────────
    private string CallerEmail => User.FindFirst("email")?.Value
                               ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                               ?? "unknown@demo.local";
    private string CallerName  => User.FindFirst("name")?.Value
                               ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                               ?? "Unknown User";

    // ── DTO mappers ───────────────────────────────────────────────────────────
    private static GeneratorLogDto ToGenDto(GeneratorLog g) => new(
        g.Id, g.Location, g.StartTime, g.EndTime, g.RuntimeHours,
        g.FuelLevelBefore, g.FuelLevelAfter, g.FuelConsumed,
        g.RunReason, g.Status, g.OutageCause, g.Notes,
        g.LoggedByEmail, g.LoggedByName, g.CreatedAt
    );

    private static DieselRecordDto ToDieselDto(DieselRecord d) => new(
        d.Id, d.RecordDate, d.RecordType,
        d.QuantityLitres, d.UnitCostNaira, d.TotalCostNaira,
        d.Supplier, d.Destination,
        d.RequestedByEmail, d.RequestedByName,
        d.ApprovedByEmail, d.ApprovedByName, d.ApprovedAt,
        d.Notes, d.CreatedAt
    );

    // ══════════════════════════════════════════════════════════════════════════
    //  STATS
    // ══════════════════════════════════════════════════════════════════════════

    // GET /api/v1/fuel/summary
    [HttpGet("summary")]
    public async Task<ActionResult<FuelPowerSummary>> Summary()
    {
        var now        = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Generator stats
        var genMonthLogs     = await db.GeneratorLogs
            .AsNoTracking()
            .Where(g => g.StartTime >= monthStart)
            .ToListAsync();

        var genStats = new GeneratorStatsDto(
            TotalRuntimeHoursThisMonth:  genMonthLogs.Where(g => g.RuntimeHours.HasValue).Sum(g => g.RuntimeHours!.Value),
            TotalFuelConsumedThisMonth:  genMonthLogs.Where(g => g.FuelConsumed.HasValue).Sum(g => g.FuelConsumed!.Value),
            OutagesThisMonth:            genMonthLogs.Count(g => g.RunReason == GeneratorRunReason.PowerOutage),
            CurrentlyRunning:            await db.GeneratorLogs.CountAsync(g => g.Status == GeneratorLogStatus.Running),
            TotalRuntimeHoursAllTime:    await db.GeneratorLogs.AsNoTracking()
                                            .Where(g => g.RuntimeHours.HasValue)
                                            .SumAsync(g => g.RuntimeHours!.Value)
        );

        // Diesel stats
        var dieselMonth = await db.DieselRecords
            .AsNoTracking()
            .Where(d => d.RecordDate >= monthStart)
            .ToListAsync();

        var purchasedAllTime = await db.DieselRecords
            .AsNoTracking()
            .Where(d => d.RecordType == DieselRecordType.Purchase)
            .SumAsync(d => (double?)d.QuantityLitres) ?? 0;

        var dispensedAllTime = await db.DieselRecords
            .AsNoTracking()
            .Where(d => d.RecordType == DieselRecordType.Dispensed)
            .SumAsync(d => (double?)d.QuantityLitres) ?? 0;

        var dieselStats = new DieselStatsDto(
            TotalPurchasedLitresThisMonth: dieselMonth.Where(d => d.RecordType == DieselRecordType.Purchase).Sum(d => d.QuantityLitres),
            TotalDispensedLitresThisMonth: dieselMonth.Where(d => d.RecordType == DieselRecordType.Dispensed).Sum(d => d.QuantityLitres),
            TotalSpendThisMonth:           dieselMonth.Where(d => d.RecordType == DieselRecordType.Purchase).Sum(d => d.TotalCostNaira),
            CurrentStockLitres:            purchasedAllTime - dispensedAllTime,
            TotalPurchasedLitresAllTime:   purchasedAllTime
        );

        return Ok(new FuelPowerSummary(genStats, dieselStats));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  GENERATOR LOGS
    // ══════════════════════════════════════════════════════════════════════════

    // GET /api/v1/fuel/generator
    [HttpGet("generator")]
    public async Task<ActionResult<GeneratorLogListResponse>> ListGeneratorLogs(
        [FromQuery] string?   location,
        [FromQuery] string?   status,
        [FromQuery] string?   runReason,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20)
    {
        var q = db.GeneratorLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(location))  q = q.Where(g => g.Location  == location);
        if (!string.IsNullOrWhiteSpace(status))    q = q.Where(g => g.Status    == status);
        if (!string.IsNullOrWhiteSpace(runReason)) q = q.Where(g => g.RunReason == runReason);
        if (from.HasValue) q = q.Where(g => g.StartTime >= from.Value);
        if (to.HasValue)   q = q.Where(g => g.StartTime <= to.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(g => g.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => ToGenDto(g))
            .ToListAsync();

        return Ok(new GeneratorLogListResponse(items, total, page, pageSize));
    }

    // POST /api/v1/fuel/generator/start
    [HttpPost("generator/start")]
    public async Task<ActionResult<GeneratorLogDto>> StartGenerator([FromBody] StartGeneratorRequest req)
    {
        var log = new GeneratorLog
        {
            Location       = req.Location,
            StartTime      = DateTime.UtcNow,
            RunReason      = req.RunReason,
            OutageCause    = req.OutageCause,
            FuelLevelBefore = req.FuelLevelBefore,
            Status         = GeneratorLogStatus.Running,
            Notes          = req.Notes,
            LoggedByEmail  = CallerEmail,
            LoggedByName   = CallerName,
        };
        db.GeneratorLogs.Add(log);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetGeneratorLog), new { id = log.Id }, ToGenDto(log));
    }

    // GET /api/v1/fuel/generator/{id}
    [HttpGet("generator/{id:guid}")]
    public async Task<ActionResult<GeneratorLogDto>> GetGeneratorLog(Guid id)
    {
        var g = await db.GeneratorLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return g is null ? NotFound() : Ok(ToGenDto(g));
    }

    // POST /api/v1/fuel/generator/{id}/stop
    [HttpPost("generator/{id:guid}/stop")]
    public async Task<ActionResult<GeneratorLogDto>> StopGenerator(Guid id, [FromBody] StopGeneratorRequest req)
    {
        var log = await db.GeneratorLogs.FirstOrDefaultAsync(x => x.Id == id);
        if (log is null) return NotFound();

        var endTime = DateTime.UtcNow;
        log.EndTime      = endTime;
        log.Status       = GeneratorLogStatus.Stopped;
        log.RuntimeHours = (endTime - log.StartTime).TotalHours;

        if (req.FuelLevelAfter.HasValue)
        {
            log.FuelLevelAfter = req.FuelLevelAfter;
            if (log.FuelLevelBefore.HasValue)
                log.FuelConsumed = log.FuelLevelBefore.Value - req.FuelLevelAfter.Value;
        }

        if (req.Notes is not null) log.Notes = req.Notes;
        log.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(ToGenDto(log));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  DIESEL RECORDS
    // ══════════════════════════════════════════════════════════════════════════

    // GET /api/v1/fuel/diesel
    [HttpGet("diesel")]
    public async Task<ActionResult<DieselRecordListResponse>> ListDieselRecords(
        [FromQuery] string?   recordType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20)
    {
        var q = db.DieselRecords.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(recordType)) q = q.Where(d => d.RecordType == recordType);
        if (from.HasValue) q = q.Where(d => d.RecordDate >= from.Value);
        if (to.HasValue)   q = q.Where(d => d.RecordDate <= to.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(d => d.RecordDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => ToDieselDto(d))
            .ToListAsync();

        return Ok(new DieselRecordListResponse(items, total, page, pageSize));
    }

    // POST /api/v1/fuel/diesel
    [HttpPost("diesel")]
    public async Task<ActionResult<DieselRecordDto>> CreateDieselRecord([FromBody] CreateDieselRecordRequest req)
    {
        var record = new DieselRecord
        {
            RecordDate      = req.RecordDate,
            RecordType      = req.RecordType,
            QuantityLitres  = req.QuantityLitres,
            UnitCostNaira   = req.UnitCostNaira,
            TotalCostNaira  = req.UnitCostNaira * (decimal)req.QuantityLitres,
            Supplier        = req.Supplier,
            Destination     = req.Destination,
            Notes           = req.Notes,
            RequestedByEmail = CallerEmail,
            RequestedByName  = CallerName,
        };

        // Auto-approve for purchases (no approval needed); Dispensed records need approval
        if (req.RecordType == DieselRecordType.Purchase)
        {
            record.ApprovedByEmail = CallerEmail;
            record.ApprovedByName  = CallerName;
            record.ApprovedAt      = DateTime.UtcNow;
        }

        db.DieselRecords.Add(record);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetDieselRecord), new { id = record.Id }, ToDieselDto(record));
    }

    // GET /api/v1/fuel/diesel/{id}
    [HttpGet("diesel/{id:guid}")]
    public async Task<ActionResult<DieselRecordDto>> GetDieselRecord(Guid id)
    {
        var d = await db.DieselRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return d is null ? NotFound() : Ok(ToDieselDto(d));
    }

    // POST /api/v1/fuel/diesel/{id}/approve
    [HttpPost("diesel/{id:guid}/approve")]
    [Authorize(Roles = "SystemAdmin,DepartmentManager,Supervisor")]
    public async Task<ActionResult<DieselRecordDto>> ApproveDieselRecord(Guid id)
    {
        var d = await db.DieselRecords.FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();

        d.ApprovedByEmail = CallerEmail;
        d.ApprovedByName  = CallerName;
        d.ApprovedAt      = DateTime.UtcNow;
        d.UpdatedAt       = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(ToDieselDto(d));
    }

    // DELETE /api/v1/fuel/diesel/{id}
    [HttpDelete("diesel/{id:guid}")]
    [Authorize(Roles = "SystemAdmin,DepartmentManager")]
    public async Task<IActionResult> DeleteDieselRecord(Guid id)
    {
        var d = await db.DieselRecords.FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        db.DieselRecords.Remove(d);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
