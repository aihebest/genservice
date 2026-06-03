using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/diesel-tank")]
[Authorize]
public class DieselTankController(
    GenServiceDbContext db,
    ILogger<DieselTankController> logger) : ControllerBase
{
    private string CallerEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";
    private string CallerName  => User.FindFirstValue(ClaimTypes.Name)  ?? "";

    private static DieselTankReadingDto ToDto(DieselTankReading r) => new(
        r.Id, r.Location, r.TankIdentifier, r.ReadingDate,
        r.TankLevelLitres, r.PreviousLevelLitres, r.ConsumptionLitres,
        r.CostPerLitreNaira, r.TotalConsumptionCostNaira,
        r.Notes, r.LoggedByEmail, r.LoggedByName, r.CreatedAt
    );

    // ── GET /api/v1/diesel-tank ──────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DieselTankQuery q)
    {
        var from  = DateTime.UtcNow.AddDays(-q.Days).Date;
        var query = db.DieselTankReadings.AsNoTracking()
                      .Where(r => r.ReadingDate >= from);

        if (!string.IsNullOrWhiteSpace(q.Location))
            query = query.Where(r => r.Location == q.Location);
        if (!string.IsNullOrWhiteSpace(q.TankIdentifier))
            query = query.Where(r => r.TankIdentifier == q.TankIdentifier);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.ReadingDate)
            .ThenByDescending(r => r.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return Ok(new { items = items.Select(ToDto), totalCount = total });
    }

    // ── GET /api/v1/diesel-tank/summary ─────────────────────────────────────
    /// <summary>Latest reading per tank + summary stats for the dashboard.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] int days = 30)
    {
        var from = DateTime.UtcNow.AddDays(-days).Date;
        var all  = await db.DieselTankReadings.AsNoTracking()
            .Where(r => r.ReadingDate >= from)
            .OrderByDescending(r => r.ReadingDate)
            .ToListAsync();

        var latestPerTank = all
            .GroupBy(r => $"{r.Location}|{r.TankIdentifier}")
            .Select(g => g.First())
            .Select(r => new
            {
                r.Location, r.TankIdentifier,
                r.TankLevelLitres, r.ReadingDate,
                r.ConsumptionLitres
            })
            .ToList();

        var totalConsumption = all.Where(r => r.ConsumptionLitres > 0).Sum(r => r.ConsumptionLitres ?? 0);
        var totalCost        = all.Sum(r => r.TotalConsumptionCostNaira ?? 0);
        var avgDailyConsumption = all.GroupBy(r => r.ReadingDate.Date)
            .Select(g => g.Sum(r => r.ConsumptionLitres ?? 0))
            .DefaultIfEmpty(0)
            .Average();

        return Ok(new
        {
            latestPerTank,
            totalConsumptionLitres = Math.Round(totalConsumption, 1),
            totalCostNaira         = totalCost,
            avgDailyConsumptionLitres = Math.Round(avgDailyConsumption, 1),
            tankCount              = latestPerTank.Count,
        });
    }

    // ── POST /api/v1/diesel-tank ─────────────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<DieselTankReadingDto>> Create(
        [FromBody] CreateDieselTankReadingRequest req)
    {
        // Auto-fetch previous reading for this tank
        var prev = await db.DieselTankReadings
            .AsNoTracking()
            .Where(r => r.Location == req.Location && r.TankIdentifier == req.TankIdentifier)
            .OrderByDescending(r => r.ReadingDate)
            .ThenByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        double? consumed = prev != null
            ? Math.Max(0, prev.TankLevelLitres - req.TankLevelLitres)
            : null;

        decimal? totalCost = null;
        if (req.CostPerLitreNaira.HasValue && consumed.HasValue && consumed > 0)
            totalCost = req.CostPerLitreNaira.Value * (decimal)consumed.Value;

        var reading = new DieselTankReading
        {
            Location                 = req.Location.Trim(),
            TankIdentifier           = req.TankIdentifier.Trim(),
            ReadingDate              = DateTime.UtcNow.Date,
            TankLevelLitres          = req.TankLevelLitres,
            PreviousLevelLitres      = prev?.TankLevelLitres,
            ConsumptionLitres        = consumed,
            CostPerLitreNaira        = req.CostPerLitreNaira,
            TotalConsumptionCostNaira= totalCost,
            Notes                    = req.Notes?.Trim(),
            LoggedByEmail            = CallerEmail,
            LoggedByName             = CallerName,
            CreatedAt                = DateTime.UtcNow,
        };

        db.DieselTankReadings.Add(reading);
        await db.SaveChangesAsync();

        logger.LogInformation("Diesel tank reading: {Location} {Tank} — {Level}L (consumed: {Consumed}L)",
            req.Location, req.TankIdentifier, req.TankLevelLitres, consumed?.ToString("0.0") ?? "N/A");

        return Ok(ToDto(reading));
    }
}
