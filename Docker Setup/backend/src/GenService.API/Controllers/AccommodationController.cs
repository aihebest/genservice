using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/accommodation")]
[Authorize]
public class AccommodationController(
    GenServiceDbContext db,
    ILogger<AccommodationController> logger) : ControllerBase
{
    private string CallerEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";
    private string CallerName  => User.FindFirstValue(ClaimTypes.Name)  ?? "";
    private string CallerRole  => User.FindFirst("role")?.Value
                               ?? User.FindFirstValue(ClaimTypes.Role)
                               ?? "Requester";
    /// <summary>Only managers/admins may correct or delete existing records.</summary>
    private bool CanEditRecords => CallerRole is "DepartmentManager" or "SystemAdmin";

    private static decimal? SumCosts(decimal? feeding, decimal? accommodation)
    {
        if (feeding is null && accommodation is null) return null;
        return (feeding ?? 0) + (accommodation ?? 0);
    }

    /// <summary>
    /// Totals the Feeding Log for a guest across the dates of their stay.
    /// Matched on name (case/whitespace-insensitive) and an entry date falling between
    /// check-in and check-out — or today, while the guest is still resident.
    /// </summary>
    private static (decimal Cost, int Count) DeriveFeeding(
        AccommodationLog r, IReadOnlyCollection<FeedingLogEntry> feeding)
    {
        var name = r.GuestName.Trim();
        var upTo = r.CheckOutDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var hits = feeding.Where(f =>
                        string.Equals(f.StaffName.Trim(), name, StringComparison.OrdinalIgnoreCase)
                        && f.EntryDate >= r.CheckInDate
                        && f.EntryDate <= upTo)
                   .ToList();
        return (hits.Sum(f => f.TotalCostNaira), hits.Count);
    }

    private static AccommodationDto ToDto(
        AccommodationLog r, IReadOnlyCollection<FeedingLogEntry>? feeding = null)
    {
        decimal? derived = null; var count = 0;
        if (feeding is not null)
        {
            var (cost, n) = DeriveFeeding(r, feeding);
            count   = n;
            derived = n > 0 ? cost : null;
        }

        // A manually entered figure always wins — it's an explicit override by a manager.
        var effectiveFeeding = r.FeedingCostNaira ?? derived;
        var effectiveTotal   = SumCosts(effectiveFeeding, r.AccommodationCostNaira);

        return new(
            r.Id,
            r.Reference,
            r.GuestName,
            r.Department,
            r.GuestHouse,
            r.Purpose,
            r.CheckInDate.ToString("yyyy-MM-dd"),
            r.CheckOutDate?.ToString("yyyy-MM-dd"),
            r.Nights,
            r.MealPlan,
            r.NumberOfMeals,
            r.FeedingCostNaira,
            r.AccommodationCostNaira,
            r.TotalCostNaira,
            r.Status,
            r.Notes,
            r.LoggedByEmail,
            r.LoggedByName,
            r.CreatedAt,
            r.UpdatedAt,
            r.LastEditedByName,
            r.LastEditedAt,
            derived,
            count,
            effectiveFeeding,
            effectiveTotal
        );
    }

    private async Task<string> NextReferenceAsync()
    {
        var yr    = DateTime.UtcNow.Year % 100;
        var count = await db.AccommodationLogs.CountAsync(r => r.CreatedAt.Year == DateTime.UtcNow.Year);
        return $"FA/{yr}/{(count + 1):D3}";
    }

    // ── GET /api/v1/accommodation ────────────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<AccommodationListResponse>> List([FromQuery] AccommodationQuery q)
    {
        var query = db.AccommodationLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.GuestHouse))
            query = query.Where(r => r.GuestHouse == q.GuestHouse);
        if (!string.IsNullOrWhiteSpace(q.Status))
            query = query.Where(r => r.Status == q.Status);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(r =>
                r.GuestName.Contains(s) || r.Reference.Contains(s) ||
                (r.Department != null && r.Department.Contains(s)));
        }
        if (!string.IsNullOrWhiteSpace(q.From) && DateOnly.TryParse(q.From, out var from))
            query = query.Where(r => r.CheckInDate >= from);
        if (!string.IsNullOrWhiteSpace(q.To) && DateOnly.TryParse(q.To, out var to))
            query = query.Where(r => r.CheckInDate <= to);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.CheckInDate)
            .ThenByDescending(r => r.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        // Pull the feeding log once and match in memory — volumes here are small and it
        // avoids a query per stay.
        var feeding = await db.FeedingLogEntries.AsNoTracking().ToListAsync();

        return Ok(new AccommodationListResponse(
            items.Select(r => ToDto(r, feeding)), total, q.Page, q.PageSize));
    }

    // ── GET /api/v1/accommodation/stats ──────────────────────────────────────
    [HttpGet("stats")]
    public async Task<ActionResult<AccommodationStatsDto>> Stats()
    {
        var now        = DateTime.UtcNow;
        var monthStart = new DateOnly(now.Year, now.Month, 1);
        var all        = await db.AccommodationLogs.AsNoTracking().ToListAsync();
        var thisMonth  = all.Where(r => r.CheckInDate >= monthStart).ToList();
        var feeding    = await db.FeedingLogEntries.AsNoTracking().ToListAsync();

        // Feeding for the month is taken from the Feeding Log entries dated this month —
        // that is the authoritative record — plus any manual per-stay figure entered for a
        // guest who has no feeding-log entries, so nothing is double-counted or lost.
        var feedingThisMonth = feeding.Where(f => f.EntryDate >= monthStart).Sum(f => f.TotalCostNaira);
        var manualOnly       = thisMonth
            .Where(r => r.FeedingCostNaira.HasValue && DeriveFeeding(r, feeding).Count == 0)
            .Sum(r => r.FeedingCostNaira!.Value);
        var feedingTotal     = feedingThisMonth + manualOnly;
        var accommodation    = thisMonth.Sum(r => r.AccommodationCostNaira ?? 0);

        return Ok(new AccommodationStatsDto(
            GuestsThisMonth:            thisMonth.Count,
            CurrentlyCheckedIn:         all.Count(r => r.Status == AccommodationStatus.CheckedIn),
            FeedingCostThisMonth:       feedingTotal,
            AccommodationCostThisMonth: accommodation,
            TotalCostThisMonth:         feedingTotal + accommodation
        ));
    }

    // ── GET /api/v1/accommodation/{id} ───────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccommodationDto>> GetById(Guid id)
    {
        var r = await db.AccommodationLogs.FindAsync(id);
        if (r is null) return NotFound();
        var feeding = await db.FeedingLogEntries.AsNoTracking().ToListAsync();
        return Ok(ToDto(r, feeding));
    }

    // ── POST /api/v1/accommodation ───────────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<AccommodationDto>> Create([FromBody] CreateAccommodationRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.GuestName))
            return BadRequest(new { message = "Guest name is required." });
        if (string.IsNullOrWhiteSpace(req.GuestHouse))
            return BadRequest(new { message = "Guest house is required." });
        if (!DateOnly.TryParse(req.CheckInDate, out var checkIn))
            return BadRequest(new { message = "Invalid check-in date. Use YYYY-MM-DD." });

        DateOnly? checkOut = null;
        if (!string.IsNullOrWhiteSpace(req.CheckOutDate) && DateOnly.TryParse(req.CheckOutDate, out var co))
            checkOut = co;

        // Derive nights from dates when not explicitly supplied.
        var nights = req.Nights;
        if (nights is null && checkOut is not null)
            nights = Math.Max(0, checkOut.Value.DayNumber - checkIn.DayNumber);

        var r = new AccommodationLog
        {
            Reference              = await NextReferenceAsync(),
            GuestName              = req.GuestName.Trim(),
            Department             = req.Department?.Trim(),
            GuestHouse             = req.GuestHouse.Trim(),
            Purpose                = req.Purpose?.Trim(),
            CheckInDate            = checkIn,
            CheckOutDate           = checkOut,
            Nights                 = nights,
            MealPlan               = req.MealPlan?.Trim(),
            NumberOfMeals          = req.NumberOfMeals,
            FeedingCostNaira       = req.FeedingCostNaira,
            AccommodationCostNaira = req.AccommodationCostNaira,
            TotalCostNaira         = SumCosts(req.FeedingCostNaira, req.AccommodationCostNaira),
            Status                 = string.IsNullOrWhiteSpace(req.Status) ? AccommodationStatus.CheckedIn : req.Status.Trim(),
            Notes                  = req.Notes?.Trim(),
            LoggedByEmail          = CallerEmail,
            LoggedByName           = CallerName,
            CreatedAt              = DateTime.UtcNow,
            UpdatedAt              = DateTime.UtcNow,
        };

        db.AccommodationLogs.Add(r);
        await db.SaveChangesAsync();
        logger.LogInformation("Accommodation {Ref}: {Guest} at {House} by {User}",
            r.Reference, r.GuestName, r.GuestHouse, CallerEmail);
        return CreatedAtAction(nameof(GetById), new { id = r.Id }, ToDto(r));
    }

    // ── PUT /api/v1/accommodation/{id} ───────────────────────────────────────
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AccommodationDto>> Update(Guid id, [FromBody] UpdateAccommodationRequest req)
    {
        if (!CanEditRecords)
            return StatusCode(403, new { message = "Only a Department Manager or System Admin can edit existing records." });

        var r = await db.AccommodationLogs.FindAsync(id);
        if (r is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(req.GuestName))  r.GuestName  = req.GuestName.Trim();
        if (!string.IsNullOrWhiteSpace(req.GuestHouse)) r.GuestHouse = req.GuestHouse.Trim();
        if (req.Department != null) r.Department = req.Department.Trim();
        if (req.Purpose    != null) r.Purpose    = req.Purpose.Trim();
        if (!string.IsNullOrWhiteSpace(req.CheckInDate) && DateOnly.TryParse(req.CheckInDate, out var ci))
            r.CheckInDate = ci;
        if (req.CheckOutDate != null)
            r.CheckOutDate = DateOnly.TryParse(req.CheckOutDate, out var co) ? co : null;
        if (req.Nights.HasValue)                 r.Nights                 = req.Nights;
        if (req.MealPlan != null)                r.MealPlan               = req.MealPlan.Trim();
        if (req.NumberOfMeals.HasValue)          r.NumberOfMeals          = req.NumberOfMeals;
        if (req.FeedingCostNaira.HasValue)       r.FeedingCostNaira       = req.FeedingCostNaira;
        if (req.AccommodationCostNaira.HasValue) r.AccommodationCostNaira = req.AccommodationCostNaira;
        if (!string.IsNullOrWhiteSpace(req.Status)) r.Status             = req.Status.Trim();
        if (req.Notes != null)                   r.Notes                  = req.Notes.Trim();

        // Recompute total and derived nights.
        r.TotalCostNaira = SumCosts(r.FeedingCostNaira, r.AccommodationCostNaira);
        if (req.Nights is null && r.CheckOutDate is not null)
            r.Nights = Math.Max(0, r.CheckOutDate.Value.DayNumber - r.CheckInDate.DayNumber);
        r.UpdatedAt        = DateTime.UtcNow;
        r.LastEditedByName = CallerName;
        r.LastEditedAt     = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/accommodation/{id}/checkout ─────────────────────────────
    /// <summary>
    /// Closes out a guest stay: records the departure date, derives the nights and
    /// flips the status to Checked Out.
    ///
    /// Deliberately NOT manager-gated — a check-out is the normal completion of the
    /// workflow, not a correction, and it is nearly always recorded after the guest
    /// has already left. Restricting it would stall day-to-day record keeping.
    /// Amending a stay's details, or re-opening a closed one, remains manager-only.
    /// </summary>
    [HttpPost("{id:guid}/checkout")]
    public async Task<ActionResult<AccommodationDto>> CheckOut(
        Guid id, [FromBody] CheckOutAccommodationRequest req)
    {
        var r = await db.AccommodationLogs.FindAsync(id);
        if (r is null) return NotFound();

        // Re-opening / re-dating a closed stay is a correction — manager territory.
        if (r.Status == AccommodationStatus.CheckedOut && !CanEditRecords)
            return StatusCode(403, new
            {
                message = "This guest is already checked out. Only a Department Manager or System Admin can change a completed stay."
            });

        var checkOut = DateOnly.FromDateTime(DateTime.UtcNow);
        if (!string.IsNullOrWhiteSpace(req.CheckOutDate))
        {
            if (!DateOnly.TryParse(req.CheckOutDate, out checkOut))
                return BadRequest(new { message = "Invalid check-out date. Use YYYY-MM-DD." });
        }

        if (checkOut < r.CheckInDate)
            return BadRequest(new { message = "Check-out date cannot be earlier than the check-in date." });

        r.CheckOutDate = checkOut;
        r.Nights       = Math.Max(0, checkOut.DayNumber - r.CheckInDate.DayNumber);
        r.Status       = AccommodationStatus.CheckedOut;

        // Costs are often only known at departure. Allow them to be filled in here
        // when nothing was captured at check-in; changing an existing figure stays
        // a manager-only action via the edit endpoint.
        if (req.FeedingCostNaira.HasValue && (r.FeedingCostNaira is null || CanEditRecords))
            r.FeedingCostNaira = req.FeedingCostNaira;
        if (req.AccommodationCostNaira.HasValue && (r.AccommodationCostNaira is null || CanEditRecords))
            r.AccommodationCostNaira = req.AccommodationCostNaira;
        r.TotalCostNaira = SumCosts(r.FeedingCostNaira, r.AccommodationCostNaira);

        if (!string.IsNullOrWhiteSpace(req.Notes))
            r.Notes = string.IsNullOrWhiteSpace(r.Notes) ? req.Notes.Trim() : $"{r.Notes}\n{req.Notes.Trim()}";

        // Stamped so it is always clear who closed the stay and when.
        r.UpdatedAt        = DateTime.UtcNow;
        r.LastEditedByName = CallerName;
        r.LastEditedAt     = DateTime.UtcNow;

        await db.SaveChangesAsync();
        logger.LogInformation("Guest checked out: {Ref} {Guest} on {Date} ({Nights} nights) by {User}",
            r.Reference, r.GuestName, checkOut, r.Nights, CallerEmail);
        return Ok(ToDto(r));
    }

    // ── DELETE /api/v1/accommodation/{id} ────────────────────────────────────
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!CanEditRecords)
            return StatusCode(403, new { message = "Only a Department Manager or System Admin can delete records." });
        var r = await db.AccommodationLogs.FindAsync(id);
        if (r is null) return NotFound();
        db.AccommodationLogs.Remove(r);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
