using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using GenService.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GenService.API.Controllers;

/// <summary>
/// Diesel supply &amp; distribution — bulk purchases into storage with a running
/// balance, and traceable distributions to vehicles or locations that reduce the
/// source batch. Over-allocation beyond the available balance is prevented.
/// </summary>
[ApiController]
[Route("api/v1/diesel-supply")]
[Authorize]
public class DieselSupplyController(
    GenServiceDbContext db,
    NotificationService notify,
    ILogger<DieselSupplyController> logger) : ControllerBase
{
    private const double LowStockThresholdLitres = 500;

    private string CallerEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";
    private string CallerName  => User.FindFirstValue(ClaimTypes.Name)  ?? "";

    // ── Reference number generators ───────────────────────────────────────────
    private async Task<string> NextSupplyRefAsync()
    {
        var yy    = DateTime.UtcNow.ToString("yy");
        var count = await db.DieselBulkSupplies.CountAsync() + 1;
        return $"DSL/{yy}/{count:000}";
    }

    private async Task<string> NextDistributionRefAsync()
    {
        var yy    = DateTime.UtcNow.ToString("yy");
        var count = await db.DieselDistributions.CountAsync() + 1;
        return $"DDT/{yy}/{count:000}";
    }

    private static DieselSupplyDto ToDto(DieselBulkSupply s) => new(
        s.Id, s.SupplyReference, s.SupplyDate, s.Vendor, s.InvoiceNumber,
        s.QuantityLitres, s.UnitPriceNaira, s.TotalCostNaira, s.StorageLocation,
        s.DeliveryDocuments, s.ReceivingOfficer, s.QuantityRemainingLitres,
        s.QuantityLitres - s.QuantityRemainingLitres,
        s.Notes, s.LoggedByEmail, s.LoggedByName, s.CreatedAt);

    private static DieselDistributionDto ToDto(DieselDistribution d) => new(
        d.Id, d.DistributionReference, d.DistributionType, d.BulkSupplyId, d.BulkSupplyReference,
        d.DistributionDate, d.QuantityLitres, d.Purpose, d.VehicleRegNo, d.Driver,
        d.OdometerReading, d.DestinationLocation, d.IssuingOfficer, d.ReceivingOfficer,
        d.RecipientAcknowledged, d.Notes, d.LoggedByEmail, d.LoggedByName, d.CreatedAt);

    // ═══════════════════════════════ SUPPLIES ═══════════════════════════════════

    // ── GET /api/v1/diesel-supply/supplies ───────────────────────────────────
    [HttpGet("supplies")]
    public async Task<IActionResult> ListSupplies([FromQuery] DieselSupplyQuery q)
    {
        var from  = DateTime.UtcNow.AddDays(-q.Days).Date;
        var query = db.DieselBulkSupplies.AsNoTracking().Where(s => s.SupplyDate >= from);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(s => s.SupplyDate)
            .ThenByDescending(s => s.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return Ok(new { items = items.Select(ToDto), totalCount = total, page = q.Page, pageSize = q.PageSize });
    }

    // ── GET /api/v1/diesel-supply/supplies/available ─────────────────────────
    /// <summary>Supply batches that still have diesel available to distribute (FIFO order).</summary>
    [HttpGet("supplies/available")]
    public async Task<IActionResult> AvailableSupplies()
    {
        var items = await db.DieselBulkSupplies.AsNoTracking()
            .Where(s => s.QuantityRemainingLitres > 0)
            .OrderBy(s => s.SupplyDate)
            .ToListAsync();
        return Ok(items.Select(ToDto));
    }

    // ── POST /api/v1/diesel-supply/supplies ──────────────────────────────────
    [HttpPost("supplies")]
    public async Task<ActionResult<DieselSupplyDto>> CreateSupply([FromBody] CreateDieselSupplyRequest req)
    {
        if (req.QuantityLitres <= 0)
            return BadRequest(new { message = "Quantity supplied must be greater than zero." });

        var supply = new DieselBulkSupply
        {
            SupplyReference         = await NextSupplyRefAsync(),
            SupplyDate              = req.SupplyDate?.Date ?? DateTime.UtcNow.Date,
            Vendor                  = req.Vendor.Trim(),
            InvoiceNumber           = req.InvoiceNumber?.Trim(),
            QuantityLitres          = req.QuantityLitres,
            UnitPriceNaira          = req.UnitPriceNaira,
            TotalCostNaira          = req.UnitPriceNaira * (decimal)req.QuantityLitres,
            StorageLocation         = req.StorageLocation?.Trim(),
            DeliveryDocuments       = req.DeliveryDocuments?.Trim(),
            ReceivingOfficer        = req.ReceivingOfficer?.Trim(),
            QuantityRemainingLitres = req.QuantityLitres,   // starts full
            Notes                   = req.Notes?.Trim(),
            LoggedByEmail           = CallerEmail,
            LoggedByName            = CallerName,
            CreatedAt               = DateTime.UtcNow,
            UpdatedAt               = DateTime.UtcNow,
        };

        db.DieselBulkSupplies.Add(supply);
        await db.SaveChangesAsync();
        logger.LogInformation("Diesel bulk supply {Ref}: {Qty} L from {Vendor}",
            supply.SupplyReference, supply.QuantityLitres, supply.Vendor);
        return CreatedAtAction(nameof(ListSupplies), ToDto(supply));
    }

    // ═════════════════════════════ DISTRIBUTIONS ════════════════════════════════

    // ── GET /api/v1/diesel-supply/distributions ──────────────────────────────
    [HttpGet("distributions")]
    public async Task<IActionResult> ListDistributions([FromQuery] DieselDistributionQuery q)
    {
        var from  = DateTime.UtcNow.AddDays(-q.Days).Date;
        var query = db.DieselDistributions.AsNoTracking().Where(d => d.DistributionDate >= from);

        if (!string.IsNullOrWhiteSpace(q.DistributionType))
            query = query.Where(d => d.DistributionType == q.DistributionType);
        if (q.BulkSupplyId.HasValue)
            query = query.Where(d => d.BulkSupplyId == q.BulkSupplyId.Value);
        if (!string.IsNullOrWhiteSpace(q.VehicleRegNo))
            query = query.Where(d => d.VehicleRegNo == q.VehicleRegNo);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.DistributionDate)
            .ThenByDescending(d => d.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return Ok(new { items = items.Select(ToDto), totalCount = total, page = q.Page, pageSize = q.PageSize });
    }

    // ── POST /api/v1/diesel-supply/distributions ─────────────────────────────
    [HttpPost("distributions")]
    public async Task<ActionResult<DieselDistributionDto>> CreateDistribution([FromBody] CreateDieselDistributionRequest req)
    {
        if (req.QuantityLitres <= 0)
            return BadRequest(new { message = "Quantity issued must be greater than zero." });

        var reference = req.BulkSupplyReference?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(reference))
            return BadRequest(new { message = "Supply batch reference is required." });

        // Match the typed/selected reference to a real batch. If found, enforce the
        // running balance and decrement it; if not, record the free-text reference as-is.
        var supply = await db.DieselBulkSupplies
            .FirstOrDefaultAsync(s => s.SupplyReference == reference);

        if (supply is not null && req.QuantityLitres > supply.QuantityRemainingLitres)
            return BadRequest(new
            {
                message = $"Cannot issue {req.QuantityLitres:0.#} L — only {supply.QuantityRemainingLitres:0.#} L "
                        + $"remaining in supply {supply.SupplyReference}."
            });

        var dist = new DieselDistribution
        {
            DistributionReference = await NextDistributionRefAsync(),
            DistributionType      = req.DistributionType.Trim(),
            BulkSupplyId          = supply?.Id ?? Guid.Empty,
            BulkSupplyReference   = supply?.SupplyReference ?? reference,
            DistributionDate      = req.DistributionDate?.Date ?? DateTime.UtcNow.Date,
            QuantityLitres        = req.QuantityLitres,
            Purpose               = req.Purpose?.Trim(),
            VehicleRegNo          = req.VehicleRegNo?.Trim(),
            Driver                = req.Driver?.Trim(),
            OdometerReading       = req.OdometerReading?.Trim(),
            DestinationLocation   = req.DestinationLocation?.Trim(),
            IssuingOfficer        = req.IssuingOfficer?.Trim(),
            ReceivingOfficer      = req.ReceivingOfficer?.Trim(),
            RecipientAcknowledged = req.RecipientAcknowledged,
            Notes                 = req.Notes?.Trim(),
            LoggedByEmail         = CallerEmail,
            LoggedByName          = CallerName,
            CreatedAt             = DateTime.UtcNow,
            UpdatedAt             = DateTime.UtcNow,
        };

        // Reduce the source batch balance atomically with the distribution insert
        // (only when the reference matched a real batch).
        if (supply is not null)
        {
            supply.QuantityRemainingLitres -= req.QuantityLitres;
            supply.UpdatedAt                = DateTime.UtcNow;
        }
        db.DieselDistributions.Add(dist);
        await db.SaveChangesAsync();

        logger.LogInformation("Diesel distribution {Ref}: {Qty} L to {Target} from {Supply}",
            dist.DistributionReference, dist.QuantityLitres,
            req.DistributionType == DieselDistributionType.Vehicle ? req.VehicleRegNo : req.DestinationLocation,
            dist.BulkSupplyReference);

        // Low-stock alert — only when supply batches actually exist (avoids a null
        // reference and spurious alerts on a system with no bulk supplies yet).
        var supplyCount = await db.DieselBulkSupplies.CountAsync();
        if (supplyCount > 0)
        {
            var available = await db.DieselBulkSupplies.SumAsync(s => s.QuantityRemainingLitres);
            if (available <= LowStockThresholdLitres)
            {
                await notify.CreateAsync(
                    title:      "⛽ Low diesel stock",
                    message:    $"Total available diesel is {available:0.#} L (threshold {LowStockThresholdLitres:0} L). Arrange resupply.",
                    type:       NotificationType.DieselLowStock,
                    module:     "Diesel",
                    entityId:   supply?.Id.ToString(),
                    targetRole: NotificationTarget.Management);
            }
        }

        return CreatedAtAction(nameof(ListDistributions), ToDto(dist));
    }

    // ── DELETE /api/v1/diesel-supply/distributions/{id} ──────────────────────
    /// <summary>Reverses a distribution and restores the diesel back to its source batch.</summary>
    [HttpDelete("distributions/{id:guid}")]
    public async Task<IActionResult> DeleteDistribution(Guid id)
    {
        var dist = await db.DieselDistributions.FindAsync(id);
        if (dist is null) return NotFound();

        var supply = await db.DieselBulkSupplies.FindAsync(dist.BulkSupplyId);
        if (supply is not null)
        {
            supply.QuantityRemainingLitres =
                Math.Min(supply.QuantityLitres, supply.QuantityRemainingLitres + dist.QuantityLitres);
            supply.UpdatedAt = DateTime.UtcNow;
        }

        db.DieselDistributions.Remove(dist);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ═══════════════════════════════ SUMMARY ════════════════════════════════════

    // ── GET /api/v1/diesel-supply/summary ────────────────────────────────────
    [HttpGet("summary")]
    public async Task<ActionResult<DieselStockSummaryDto>> Summary()
    {
        var supplies = await db.DieselBulkSupplies.AsNoTracking().ToListAsync();
        var totalSupplied  = supplies.Sum(s => s.QuantityLitres);
        var available      = supplies.Sum(s => s.QuantityRemainingLitres);

        return Ok(new DieselStockSummaryDto(
            totalSupplied,
            totalSupplied - available,
            available,
            supplies.Sum(s => s.TotalCostNaira),
            supplies.Count(s => s.QuantityRemainingLitres > 0)));
    }
}
