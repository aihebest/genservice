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
/// Electricity management — PHED (postpaid) and Prepaid (token) purchases under one
/// interface, with a running unit balance per location and low-balance alerts.
/// </summary>
[ApiController]
[Route("api/v1/electricity")]
[Authorize]
public class ElectricityController(
    GenServiceDbContext db,
    NotificationService notify,
    ILogger<ElectricityController> logger) : ControllerBase
{
    private string CallerEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";
    private string CallerName  => User.FindFirstValue(ClaimTypes.Name)  ?? "";
    private string CallerRole  => User.FindFirst("role")?.Value
                               ?? User.FindFirstValue(ClaimTypes.Role)
                               ?? "Requester";
    /// <summary>Only managers/admins may correct or delete existing records.</summary>
    private bool CanEditRecords => CallerRole is "DepartmentManager" or "SystemAdmin";

    private static ElectricityPurchaseDto ToDto(ElectricityPurchase p) => new(
        p.Id, p.PurchaseType, p.Location, p.PurchaseDate, p.Vendor,
        p.AmountNaira, p.UnitsKwh, p.PaymentReference, p.TokenNumber,
        p.MeterReadingKwh, p.ReceiptAttachment,
        p.LowBalanceThresholdKwh, p.BalanceAfterKwh, p.Status,
        p.Notes, p.LoggedByEmail, p.LoggedByName, p.CreatedAt,
        p.LastEditedByName, p.LastEditedAt,
        p.PreviousMeterReading, p.CurrentMeterReading,
        p.UnitsKwh > 0 ? (decimal?)Math.Round(p.AmountNaira / (decimal)p.UnitsKwh, 2) : null);

    /// <summary>
    /// PHED units consumed = current − previous reading, when both are known and
    /// the user didn't enter units directly. Never negative (a meter replacement
    /// resets the count; the user should enter units by hand in that case).
    /// </summary>
    private static double ResolveUnits(double entered, double? previous, double? current)
        => entered > 0 ? entered
         : previous is double pr && current is double cu && cu >= pr ? cu - pr
         : entered;

    private static string StatusFor(double balance, double threshold) =>
        balance <= 0        ? ElectricityStatus.Depleted
      : balance <= threshold ? ElectricityStatus.LowBalance
      :                        ElectricityStatus.Active;

    // ── GET /api/v1/electricity ──────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ElectricityQuery q)
    {
        var from  = DateTime.UtcNow.AddDays(-q.Days).Date;
        var query = db.ElectricityPurchases.AsNoTracking()
                      .Where(p => p.PurchaseDate >= from);

        if (!string.IsNullOrWhiteSpace(q.PurchaseType))
            query = query.Where(p => p.PurchaseType == q.PurchaseType);
        if (!string.IsNullOrWhiteSpace(q.Location))
            query = query.Where(p => p.Location == q.Location);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.PurchaseDate)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return Ok(new { items = items.Select(ToDto), totalCount = total, page = q.Page, pageSize = q.PageSize });
    }

    // ── GET /api/v1/electricity/balances ─────────────────────────────────────
    /// <summary>Latest running balance + status per location.</summary>
    [HttpGet("balances")]
    public async Task<ActionResult<IEnumerable<ElectricityBalanceDto>>> Balances()
    {
        var all = await db.ElectricityPurchases.AsNoTracking().ToListAsync();

        var byLocation = all
            .GroupBy(p => p.Location)
            .Select(g =>
            {
                var latest = g.OrderByDescending(p => p.PurchaseDate)
                              .ThenByDescending(p => p.CreatedAt)
                              .First();
                return new ElectricityBalanceDto(
                    g.Key,
                    latest.BalanceAfterKwh,
                    latest.LowBalanceThresholdKwh,
                    latest.Status,
                    g.Sum(p => p.AmountNaira),
                    g.Sum(p => p.UnitsKwh),
                    g.Max(p => p.PurchaseDate));
            })
            .OrderBy(b => b.BalanceKwh)
            .ToList();

        return Ok(byLocation);
    }

    // ── POST /api/v1/electricity ─────────────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<ElectricityPurchaseDto>> Create(
        [FromBody] CreateElectricityPurchaseRequest req)
    {
        var location  = req.Location.Trim();
        var threshold = req.LowBalanceThresholdKwh > 0 ? req.LowBalanceThresholdKwh : 50;

        // Previous running balance for this location (0 if first record).
        var last = await db.ElectricityPurchases.AsNoTracking()
            .Where(p => p.Location == location)
            .OrderByDescending(p => p.PurchaseDate)
            .ThenByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        // PHED meter log: previous reading defaults to the last PHED reading here.
        var isPhed = string.Equals(req.PurchaseType.Trim(), ElectricityType.PHED, StringComparison.OrdinalIgnoreCase);
        double? prevReading = null, currReading = null;
        if (isPhed)
        {
            currReading = req.CurrentMeterReading;
            prevReading = req.PreviousMeterReading ?? await db.ElectricityPurchases.AsNoTracking()
                .Where(p => p.Location == location && p.PurchaseType == ElectricityType.PHED
                         && p.CurrentMeterReading != null)
                .OrderByDescending(p => p.PurchaseDate).ThenByDescending(p => p.CreatedAt)
                .Select(p => p.CurrentMeterReading)
                .FirstOrDefaultAsync();
            if (currReading is double c && prevReading is double pv && c < pv)
                return BadRequest(new { message = $"Current reading ({c:N0}) is lower than the previous reading ({pv:N0})." });
        }
        var units = ResolveUnits(req.UnitsKwh, prevReading, currReading);

        // A prepaid meter reading reflects the true remaining units, so it overrides
        // the running total. Otherwise the purchase tops up the previous balance.
        double balance = req.MeterReadingKwh
            ?? ((last?.BalanceAfterKwh ?? 0) + units);

        var status = StatusFor(balance, threshold);

        var purchase = new ElectricityPurchase
        {
            PurchaseType           = req.PurchaseType.Trim(),
            Location               = location,
            PurchaseDate           = req.PurchaseDate?.Date ?? DateTime.UtcNow.Date,
            Vendor                 = req.Vendor?.Trim(),
            AmountNaira            = req.AmountNaira,
            UnitsKwh               = units,
            PreviousMeterReading   = prevReading,
            CurrentMeterReading    = currReading,
            PaymentReference       = req.PaymentReference?.Trim(),
            TokenNumber            = req.TokenNumber?.Trim(),
            MeterReadingKwh        = req.MeterReadingKwh,
            ReceiptAttachment      = req.ReceiptAttachment?.Trim(),
            LowBalanceThresholdKwh = threshold,
            BalanceAfterKwh        = balance,
            Status                 = status,
            Notes                  = req.Notes?.Trim(),
            LoggedByEmail          = CallerEmail,
            LoggedByName           = CallerName,
            CreatedAt              = DateTime.UtcNow,
            UpdatedAt              = DateTime.UtcNow,
        };

        db.ElectricityPurchases.Add(purchase);
        await db.SaveChangesAsync();

        if (status != ElectricityStatus.Active)
        {
            await notify.CreateAsync(
                title:      $"⚡ Low electricity balance: {location}",
                message:    $"{location} is at {balance:0.#} kWh (threshold {threshold:0.#} kWh). "
                          + (status == ElectricityStatus.Depleted ? "Balance is depleted — recharge immediately." : "Please arrange a recharge."),
                type:       NotificationType.ElectricityLowBalance,
                module:     "Electricity",
                entityId:   purchase.Id.ToString(),
                refNumber:  location,
                targetRole: NotificationTarget.Management);
        }

        logger.LogInformation("Electricity {Type} purchase logged: {Location} — {Units} kWh, balance {Bal} kWh",
            purchase.PurchaseType, location, units, balance);

        return CreatedAtAction(nameof(List), ToDto(purchase));
    }

    // ── DELETE /api/v1/electricity/{id} ──────────────────────────────────────
    // ── PUT /api/v1/electricity/{id} ────────────────────────────────────────
    /// <summary>Manager-only correction of an electricity purchase, with edit audit stamp.</summary>
    // ── GET /api/v1/electricity/last-phed-reading?location= ─────────────────
    /// <summary>Latest PHED meter reading at a location — pre-fills "Previous reading".</summary>
    [HttpGet("last-phed-reading")]
    public async Task<ActionResult<object>> LastPhedReading([FromQuery] string location)
    {
        var loc = (location ?? "").Trim();
        var last = await db.ElectricityPurchases.AsNoTracking()
            .Where(p => p.Location == loc && p.PurchaseType == ElectricityType.PHED && p.CurrentMeterReading != null)
            .OrderByDescending(p => p.PurchaseDate).ThenByDescending(p => p.CreatedAt)
            .Select(p => new { p.CurrentMeterReading, p.PurchaseDate })
            .FirstOrDefaultAsync();
        return Ok(new { reading = last?.CurrentMeterReading, date = last?.PurchaseDate });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ElectricityPurchaseDto>> Update(
        Guid id, [FromBody] CreateElectricityPurchaseRequest req)
    {
        if (!CanEditRecords)
            return StatusCode(403, new { message = "Only a Department Manager or System Admin can edit existing records." });

        var p = await db.ElectricityPurchases.FindAsync(id);
        if (p is null) return NotFound();

        p.PurchaseType           = req.PurchaseType.Trim();
        p.Location               = req.Location.Trim();
        p.Vendor                 = req.Vendor?.Trim();
        p.AmountNaira            = req.AmountNaira;
        p.PreviousMeterReading   = req.PreviousMeterReading;
        p.CurrentMeterReading    = req.CurrentMeterReading;
        p.UnitsKwh               = ResolveUnits(req.UnitsKwh, req.PreviousMeterReading, req.CurrentMeterReading);
        p.PaymentReference       = req.PaymentReference?.Trim();
        p.TokenNumber            = req.TokenNumber?.Trim();
        p.MeterReadingKwh        = req.MeterReadingKwh;
        p.Notes                  = req.Notes?.Trim() ?? p.Notes;
        if (req.PurchaseDate.HasValue) p.PurchaseDate = req.PurchaseDate.Value.Date;
        p.LowBalanceThresholdKwh = req.LowBalanceThresholdKwh;
        p.ReceiptAttachment      = req.ReceiptAttachment?.Trim() ?? p.ReceiptAttachment;
        p.UpdatedAt        = DateTime.UtcNow;
        p.LastEditedByName = CallerName;
        p.LastEditedAt     = DateTime.UtcNow;

        await db.SaveChangesAsync();
        logger.LogInformation("Electricity purchase {Id} edited by {User}", id, CallerEmail);
        return Ok(ToDto(p));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!CanEditRecords)
            return StatusCode(403, new { message = "Only a Department Manager or System Admin can delete records." });
        var p = await db.ElectricityPurchases.FindAsync(id);
        if (p is null) return NotFound();
        db.ElectricityPurchases.Remove(p);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
