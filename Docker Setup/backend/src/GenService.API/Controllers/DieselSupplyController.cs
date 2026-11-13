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
    private string CallerRole  => User.FindFirst("role")?.Value
                               ?? User.FindFirstValue(ClaimTypes.Role)
                               ?? "Requester";
    /// <summary>Only managers/admins may delete existing records.</summary>
    private bool CanEditRecords => CallerRole is "DepartmentManager" or "SystemAdmin";

    // ── Reference number generators ───────────────────────────────────────────
    private async Task<string> NextSupplyRefAsync()
    {
        // Highest existing number + 1 — safe when records have been deleted (see RefNumbers).
        var prefix = $"DSL/{DateTime.UtcNow.Year % 100}/";
        var used   = await db.DieselBulkSupplies.Where(r => r.SupplyReference.StartsWith(prefix)).Select(r => r.SupplyReference).ToListAsync();
        return RefNumbers.Next(prefix, used, 3);
    }

    private async Task<string> NextDistributionRefAsync()
    {
        // Highest existing number + 1 — safe when records have been deleted (see RefNumbers).
        var prefix = $"DDT/{DateTime.UtcNow.Year % 100}/";
        var used   = await db.DieselDistributions.Where(r => r.DistributionReference.StartsWith(prefix)).Select(r => r.DistributionReference).ToListAsync();
        return RefNumbers.Next(prefix, used, 3);
    }

    private static DieselSupplyDto ToDto(DieselBulkSupply s) => new(
        s.Id, s.SupplyReference, s.SupplyDate, s.Vendor, s.InvoiceNumber,
        s.QuantityLitres, s.UnitPriceNaira, s.TotalCostNaira, s.StorageLocation,
        s.DeliveryDocuments, s.ReceivingOfficer, s.QuantityRemainingLitres,
        s.QuantityLitres - s.QuantityRemainingLitres,
        s.Notes, s.LoggedByEmail, s.LoggedByName, s.CreatedAt,
        s.LastEditedByName, s.LastEditedAt);

    private static DieselDistributionDto ToDto(DieselDistribution d) => new(
        d.Id, d.DistributionReference, d.DistributionType, d.SupplyType, d.BulkSupplyId, d.BulkSupplyReference,
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

    // ── PUT /api/v1/diesel-supply/supplies/{id} ─────────────────────────────
    /// <summary>
    /// Department Manager correction of a bulk supply batch.
    ///
    /// Litres already distributed from the batch are kept intact: the remaining
    /// balance becomes new quantity − already distributed, so the running stock
    /// stays correct. The quantity can't be reduced below what has gone out.
    /// </summary>
    [HttpPut("supplies/{id:guid}")]
    public async Task<ActionResult<DieselSupplyDto>> UpdateSupply(Guid id, [FromBody] UpdateDieselSupplyRequest req)
    {
        if (!CanEditRecords)
            return StatusCode(403, new { message = "Only a Department Manager or System Admin can edit bulk supplies." });

        var s = await db.DieselBulkSupplies.FindAsync(id);
        if (s is null) return NotFound();

        if (req.QuantityLitres is double qty)
        {
            var distributed = s.QuantityLitres - s.QuantityRemainingLitres;
            if (qty <= 0)
                return BadRequest(new { message = "Quantity supplied must be greater than zero." });
            if (qty < distributed)
                return BadRequest(new { message =
                    $"{distributed:0.#} L has already been distributed from {s.SupplyReference}; the quantity can't be less than that." });
            s.QuantityLitres          = qty;
            s.QuantityRemainingLitres = qty - distributed;
        }
        if (!string.IsNullOrWhiteSpace(req.Vendor)) s.Vendor = req.Vendor.Trim();
        if (req.UnitPriceNaira is decimal price)   s.UnitPriceNaira = price;
        if (req.SupplyDate     is DateTime date)   s.SupplyDate     = date.Date;
        if (req.InvoiceNumber     is not null) s.InvoiceNumber     = req.InvoiceNumber.Trim();
        if (req.StorageLocation   is not null) s.StorageLocation   = req.StorageLocation.Trim();
        if (req.DeliveryDocuments is not null) s.DeliveryDocuments = req.DeliveryDocuments.Trim();
        if (req.ReceivingOfficer  is not null) s.ReceivingOfficer  = req.ReceivingOfficer.Trim();
        if (req.Notes             is not null) s.Notes             = req.Notes.Trim();

        s.TotalCostNaira   = s.UnitPriceNaira * (decimal)s.QuantityLitres;
        s.UpdatedAt        = DateTime.UtcNow;
        s.LastEditedByName = CallerName;
        s.LastEditedAt     = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Bulk supply {Ref} edited by {User}", s.SupplyReference, CallerEmail);
        return Ok(ToDto(s));
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

        // Previous issue / distance / km-per-litre for each vehicle row. Uses the
        // vehicle's full history, not just this page, so the first row on a page
        // still finds its previous issue.
        var regs = items.Where(d => d.DistributionType == DieselDistributionType.Vehicle && !string.IsNullOrWhiteSpace(d.VehicleRegNo))
                        .Select(d => RegKey(d.VehicleRegNo)).ToHashSet();
        var consumption = regs.Count == 0
            ? new Dictionary<Guid, Consumption>()
            : ComputeConsumption(await db.DieselDistributions.AsNoTracking()
                  .Where(d => d.DistributionType == DieselDistributionType.Vehicle && d.VehicleRegNo != null)
                  .ToListAsync(), regs);

        var dtos = items.Select(d => consumption.TryGetValue(d.Id, out var c)
            ? ToDto(d) with
              {
                  PreviousDistributionDate = c.PrevDate, PreviousQuantityLitres = c.PrevLitres,
                  PreviousOdometer = c.PrevOdo, DistanceKm = c.Distance, KmPerLitre = c.KmPerLitre,
              }
            : ToDto(d));

        return Ok(new { items = dtos, totalCount = total, page = q.Page, pageSize = q.PageSize });
    }

    // ── GET /api/v1/diesel-supply/distributions/vehicle-summary ─────────────
    /// <summary>
    /// Per-vehicle diesel use: issues, litres, distance covered and average km per
    /// litre — for spotting vehicles drawing more fuel than they should.
    /// </summary>
    [HttpGet("distributions/vehicle-summary")]
    public async Task<ActionResult<IEnumerable<VehicleDieselSummaryDto>>> VehicleSummary([FromQuery] int days = 90)
    {
        var from = DateTime.UtcNow.AddDays(-Math.Max(1, days)).Date;
        var all = await db.DieselDistributions.AsNoTracking()
            .Where(d => d.DistributionType == DieselDistributionType.Vehicle && d.VehicleRegNo != null)
            .ToListAsync();
        var consumption = ComputeConsumption(all, null);

        var rows = all
            .Where(d => d.DistributionDate >= from)
            .GroupBy(d => RegKey(d.VehicleRegNo))
            .Select(g =>
            {
                var list  = g.OrderBy(d => d.DistributionDate).ThenBy(d => d.CreatedAt).ToList();
                var pairs = list.Select(d => consumption.GetValueOrDefault(d.Id))
                                .Where(c => c is { Distance: not null, PrevLitres: > 0 }).ToList();
                double? dist   = pairs.Count > 0 ? pairs.Sum(c => c!.Distance!.Value) : null;
                double? litres = pairs.Count > 0 ? pairs.Sum(c => c!.PrevLitres!.Value) : null;
                var last = list[^1];
                return new VehicleDieselSummaryDto(
                    last.VehicleRegNo!.Trim(), list.Count, list.Sum(d => d.QuantityLitres),
                    last.DistributionDate, ParseOdo(last.OdometerReading),
                    dist, litres,
                    dist is double km && litres is double l && l > 0 ? Math.Round(km / l, 2) : null);
            })
            .OrderByDescending(r => r.TotalLitres)
            .ToList();
        return Ok(rows);
    }

    // ── Vehicle consumption helpers ─────────────────────────────────────────
    private sealed record Consumption(
        DateTime? PrevDate, double? PrevLitres, double? PrevOdo, double? Distance, double? KmPerLitre);

    /// <summary>Registration as a comparison key — "PHC 178 AM" and "phc178am" are the same vehicle.</summary>
    private static string RegKey(string? reg) =>
        new((reg ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static double? ParseOdo(string? s)
    {
        var digits = new string((s ?? "").Where(c => char.IsDigit(c) || c == '.').ToArray());
        return double.TryParse(digits, System.Globalization.NumberStyles.Any,
                   System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;
    }

    /// <summary>
    /// Each vehicle issue compared with the one before it: distance = odometer now −
    /// odometer then; km/L = distance ÷ litres issued then. A non-increasing
    /// odometer (typo or reset) yields no distance rather than a nonsense figure.
    /// </summary>
    private static Dictionary<Guid, Consumption> ComputeConsumption(
        List<DieselDistribution> history, HashSet<string>? onlyRegs)
    {
        var result = new Dictionary<Guid, Consumption>();
        foreach (var g in history.GroupBy(d => RegKey(d.VehicleRegNo)))
        {
            if (onlyRegs is not null && !onlyRegs.Contains(g.Key)) continue;
            DieselDistribution? prev = null;
            foreach (var d in g.OrderBy(d => d.DistributionDate).ThenBy(d => d.CreatedAt))
            {
                if (prev is not null)
                {
                    var now = ParseOdo(d.OdometerReading);
                    var then = ParseOdo(prev.OdometerReading);
                    double? dist = now is double n && then is double t && n > t ? n - t : null;
                    double? kmpl = dist is double km && prev.QuantityLitres > 0
                        ? Math.Round(km / prev.QuantityLitres, 2) : null;
                    result[d.Id] = new Consumption(prev.DistributionDate, prev.QuantityLitres, then, dist, kmpl);
                }
                prev = d;
            }
        }
        return result;
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

        // The distribution MUST draw from a real bulk-supply batch so the running
        // balance always stays accurate. A reference that doesn't match a batch is
        // rejected rather than recorded as free-text.
        var supply = await db.DieselBulkSupplies
            .FirstOrDefaultAsync(s => s.SupplyReference == reference);

        if (supply is null)
            return BadRequest(new
            {
                message = $"Supply batch '{reference}' was not found. Select an existing bulk-supply batch to distribute from."
            });

        if (req.QuantityLitres > supply.QuantityRemainingLitres)
            return BadRequest(new
            {
                message = $"Cannot issue {req.QuantityLitres:0.#} L — only {supply.QuantityRemainingLitres:0.#} L "
                        + $"remaining in supply {supply.SupplyReference}."
            });

        var dist = new DieselDistribution
        {
            DistributionReference = await NextDistributionRefAsync(),
            DistributionType      = req.DistributionType.Trim(),
            SupplyType            = string.IsNullOrWhiteSpace(req.SupplyType) ? "Regular" : req.SupplyType.Trim(),
            BulkSupplyId          = supply.Id,
            BulkSupplyReference   = supply.SupplyReference,
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

        // Reduce the source batch balance atomically with the distribution insert.
        supply.QuantityRemainingLitres -= req.QuantityLitres;
        supply.UpdatedAt                = DateTime.UtcNow;
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
                    entityId:   supply.Id.ToString(),
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
        if (!CanEditRecords)
            return StatusCode(403, new { message = "Only a Department Manager or System Admin can delete records." });
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
