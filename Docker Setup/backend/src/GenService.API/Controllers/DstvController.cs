using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GenService.API.Controllers;

/// <summary>
/// DStv subscription register — auto-calculates expiry and status; renewal reminders
/// (7/3/1 days) are driven by the background MaintenanceReminderService.
/// </summary>
[ApiController]
[Route("api/v1/dstv")]
[Authorize]
public class DstvController(
    GenServiceDbContext db,
    ILogger<DstvController> logger) : ControllerBase
{
    private string CallerEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";
    private string CallerName  => User.FindFirstValue(ClaimTypes.Name)  ?? "";
    private string CallerRole  => User.FindFirst("role")?.Value
                               ?? User.FindFirstValue(ClaimTypes.Role)
                               ?? "Requester";
    /// <summary>Only managers/admins may correct or delete existing records.</summary>
    private bool CanEditRecords => CallerRole is "DepartmentManager" or "SystemAdmin";

    private static int DaysToExpiry(DateTime expiry) =>
        (int)Math.Ceiling((expiry.Date - DateTime.UtcNow.Date).TotalDays);

    public static string StatusFor(DateTime expiry)
    {
        var days = DaysToExpiry(expiry);
        return days < 0  ? DstvStatus.Expired
             : days <= 7 ? DstvStatus.ExpiringSoon
             :             DstvStatus.Active;
    }

    private static DstvSubscriptionDto ToDto(DstvSubscription s) => new(
        s.Id, s.DecoderNumber, s.Location, s.Package, s.StartDate,
        s.DurationMonths, s.ExpiryDate, DaysToExpiry(s.ExpiryDate),
        s.AmountNaira, s.PaymentMethod, s.Vendor, s.ReceiptAttachment,
        s.Status, s.Notes, s.LoggedByEmail, s.LoggedByName, s.CreatedAt,
        s.LastEditedByName, s.LastEditedAt);

    // ── GET /api/v1/dstv ─────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DstvQuery q)
    {
        var items = await db.DstvSubscriptions.AsNoTracking().ToListAsync();

        // Recompute status on read so the list always reflects live expiry.
        foreach (var s in items) s.Status = StatusFor(s.ExpiryDate);

        var filtered = items.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(q.Status))
            filtered = filtered.Where(s => s.Status == q.Status);
        if (!string.IsNullOrWhiteSpace(q.Location))
            filtered = filtered.Where(s => s.Location == q.Location);

        var ordered = filtered.OrderBy(s => s.ExpiryDate).ToList();
        var total   = ordered.Count;
        var paged   = ordered.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize);

        return Ok(new { items = paged.Select(ToDto), totalCount = total, page = q.Page, pageSize = q.PageSize });
    }

    // ── GET /api/v1/dstv/upcoming ────────────────────────────────────────────
    /// <summary>Subscriptions expiring within the next {days} days (default 30) or already expired.</summary>
    [HttpGet("upcoming")]
    public async Task<IActionResult> Upcoming([FromQuery] int days = 30)
    {
        var all = await db.DstvSubscriptions.AsNoTracking().ToListAsync();
        var upcoming = all
            .Where(s => DaysToExpiry(s.ExpiryDate) <= days)
            .OrderBy(s => s.ExpiryDate)
            .Select(ToDto)
            .ToList();
        return Ok(upcoming);
    }

    // ── POST /api/v1/dstv ────────────────────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<DstvSubscriptionDto>> Create(
        [FromBody] CreateDstvSubscriptionRequest req)
    {
        var start = req.StartDate?.Date ?? DateTime.UtcNow.Date;

        // Prefer the explicit End Date; otherwise fall back to duration months.
        DateTime expiry;
        int months;
        if (req.EndDate.HasValue)
        {
            expiry = req.EndDate.Value.Date;
            months = Math.Max(1, (int)Math.Round((expiry - start).TotalDays / 30.0));
        }
        else
        {
            months = req.DurationMonths > 0 ? req.DurationMonths : 1;
            expiry = start.AddMonths(months);
        }

        var sub = new DstvSubscription
        {
            DecoderNumber     = req.DecoderNumber.Trim(),
            Location          = req.Location.Trim(),
            Package           = req.Package.Trim(),
            StartDate         = start,
            DurationMonths    = months,
            ExpiryDate        = expiry,
            AmountNaira       = req.AmountNaira,
            PaymentMethod     = req.PaymentMethod?.Trim(),
            Vendor            = req.Vendor?.Trim(),
            ReceiptAttachment = req.ReceiptAttachment?.Trim(),
            Status            = StatusFor(expiry),
            Notes             = req.Notes?.Trim(),
            LoggedByEmail     = CallerEmail,
            LoggedByName      = CallerName,
            CreatedAt         = DateTime.UtcNow,
            UpdatedAt         = DateTime.UtcNow,
        };

        db.DstvSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        logger.LogInformation("DStv subscription added: {Decoder} @ {Location} expiring {Expiry:d}",
            sub.DecoderNumber, sub.Location, expiry);

        return CreatedAtAction(nameof(List), ToDto(sub));
    }

    // ── POST /api/v1/dstv/{id}/renew ─────────────────────────────────────────
    [HttpPost("{id:guid}/renew")]
    public async Task<ActionResult<DstvSubscriptionDto>> Renew(
        Guid id, [FromBody] RenewDstvSubscriptionRequest req)
    {
        var sub = await db.DstvSubscriptions.FindAsync(id);
        if (sub is null) return NotFound();

        var months = req.DurationMonths > 0 ? req.DurationMonths : 1;
        // Extend from the later of today or the current expiry so no coverage is lost.
        var baseDate = req.RenewalDate?.Date
                       ?? (sub.ExpiryDate > DateTime.UtcNow.Date ? sub.ExpiryDate : DateTime.UtcNow.Date);

        sub.StartDate           = baseDate;
        sub.DurationMonths      = months;
        sub.ExpiryDate          = baseDate.AddMonths(months);
        sub.AmountNaira         = req.AmountNaira;
        if (req.PaymentMethod     is not null) sub.PaymentMethod     = req.PaymentMethod.Trim();
        if (req.Vendor            is not null) sub.Vendor            = req.Vendor.Trim();
        if (req.ReceiptAttachment is not null) sub.ReceiptAttachment = req.ReceiptAttachment.Trim();
        if (req.Notes             is not null) sub.Notes             = req.Notes.Trim();
        sub.Status              = StatusFor(sub.ExpiryDate);
        sub.LastReminderDaysOut = null;   // reset reminder cycle
        sub.ExpiredNotified     = false;
        sub.UpdatedAt           = DateTime.UtcNow;

        await db.SaveChangesAsync();

        logger.LogInformation("DStv subscription renewed: {Decoder} — new expiry {Expiry:d}",
            sub.DecoderNumber, sub.ExpiryDate);

        return Ok(ToDto(sub));
    }

    // ── DELETE /api/v1/dstv/{id} ─────────────────────────────────────────────
    // ── PUT /api/v1/dstv/{id} ───────────────────────────────────────────────
    /// <summary>Manager-only correction of a DStv subscription, with edit audit stamp.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DstvSubscriptionDto>> Update(
        Guid id, [FromBody] CreateDstvSubscriptionRequest req)
    {
        if (!CanEditRecords)
            return StatusCode(403, new { message = "Only a Department Manager or System Admin can edit existing records." });

        var s = await db.DstvSubscriptions.FindAsync(id);
        if (s is null) return NotFound();

        s.DecoderNumber  = req.DecoderNumber.Trim();
        s.Location       = req.Location.Trim();
        s.Package        = req.Package.Trim();
        s.DurationMonths = req.DurationMonths;
        s.AmountNaira    = req.AmountNaira;
        s.PaymentMethod  = req.PaymentMethod?.Trim();
        s.Vendor         = req.Vendor?.Trim();
        s.Notes          = req.Notes?.Trim() ?? s.Notes;
        if (req.StartDate.HasValue) s.StartDate = req.StartDate.Value.Date;
        // Explicit end date wins; otherwise derive from duration (mirrors Create).
        if (req.EndDate.HasValue)           s.ExpiryDate = req.EndDate.Value.Date;
        else if (req.DurationMonths > 0)    s.ExpiryDate = s.StartDate.AddMonths(req.DurationMonths);
        s.ReceiptAttachment = req.ReceiptAttachment?.Trim() ?? s.ReceiptAttachment;
        s.Status           = StatusFor(s.ExpiryDate);
        s.UpdatedAt        = DateTime.UtcNow;
        s.LastEditedByName = CallerName;
        s.LastEditedAt     = DateTime.UtcNow;

        await db.SaveChangesAsync();
        logger.LogInformation("DStv subscription {Id} edited by {User}", id, CallerEmail);
        return Ok(ToDto(s));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!CanEditRecords)
            return StatusCode(403, new { message = "Only a Department Manager or System Admin can delete records." });
        var s = await db.DstvSubscriptions.FindAsync(id);
        if (s is null) return NotFound();
        db.DstvSubscriptions.Remove(s);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
