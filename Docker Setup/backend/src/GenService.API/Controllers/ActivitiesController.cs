using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/activities")]
[Authorize]
public class ActivitiesController(GenServiceDbContext db) : ControllerBase
{
    // ── Helpers ───────────────────────────────────────────────────────────────
    private string CallerEmail => User.FindFirst("email")?.Value
                               ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                               ?? "unknown@demo.local";
    private string CallerName  => User.FindFirst("name")?.Value
                               ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                               ?? "Unknown User";
    private string CallerRole  => User.FindFirst("role")?.Value
                               ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                               ?? "Requester";

    private static ActivityDto ToDto(StaffActivity a) => new(
        a.Id, a.StaffEmail, a.StaffName, a.ActivityDescription,
        a.Location, a.Category, a.Status, a.IsProxy,
        a.LoggedByEmail, a.LoggedByName, a.Notes,
        a.StartedAt, a.UpdatedAt, a.CompletedAt
    );

    // ── GET /api/v1/activities ─────────────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<ActivityListResponse>> List(
        [FromQuery] string? status,
        [FromQuery] string? category,
        [FromQuery] string? staffEmail,
        [FromQuery] string? search,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20)
    {
        var q = db.StaffActivities.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(a => a.Status == status);

        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(a => a.Category == category);

        if (!string.IsNullOrWhiteSpace(staffEmail))
            q = q.Where(a => a.StaffEmail == staffEmail);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(a =>
                a.StaffName.ToLower().Contains(s) ||
                a.ActivityDescription.ToLower().Contains(s) ||
                a.Location.ToLower().Contains(s));
        }

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(a => a.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => ToDto(a))
            .ToListAsync();

        return Ok(new ActivityListResponse(items, total, page, pageSize));
    }

    // ── GET /api/v1/activities/active ──────────────────────────────────────
    // Returns all currently-Active activities (the live dashboard feed)
    [HttpGet("active")]
    public async Task<ActionResult<IReadOnlyList<ActivityDto>>> GetActive()
    {
        var items = await db.StaffActivities
            .AsNoTracking()
            .Where(a => a.Status == ActivityStatus.Active)
            .OrderBy(a => a.StaffName)
            .Select(a => ToDto(a))
            .ToListAsync();

        return Ok(items);
    }

    // ── GET /api/v1/activities/{id} ────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ActivityDto>> GetById(Guid id)
    {
        var a = await db.StaffActivities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return a is null ? NotFound() : Ok(ToDto(a));
    }

    // ── POST /api/v1/activities ────────────────────────────────────────────
    // Log own activity (caller IS the staff member)
    [HttpPost]
    public async Task<ActionResult<ActivityDto>> Log([FromBody] LogActivityRequest req)
    {
        // Complete any previously Active entry for this staff member
        var existing = await db.StaffActivities
            .Where(a => a.StaffEmail == CallerEmail && a.Status == ActivityStatus.Active)
            .ToListAsync();
        foreach (var old in existing)
        {
            old.Status      = ActivityStatus.Completed;
            old.CompletedAt = DateTime.UtcNow;
            old.UpdatedAt   = DateTime.UtcNow;
        }

        var activity = new StaffActivity
        {
            StaffEmail          = CallerEmail,
            StaffName           = CallerName,
            ActivityDescription = req.ActivityDescription,
            Category            = req.Category,
            Location            = req.Location ?? "",
            Notes               = req.Notes,
            IsProxy             = false,
            LoggedByEmail       = CallerEmail,
            LoggedByName        = CallerName,
        };

        db.StaffActivities.Add(activity);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = activity.Id }, ToDto(activity));
    }

    // ── POST /api/v1/activities/proxy ──────────────────────────────────────
    // Supervisor / Manager logs on behalf of a technician
    [HttpPost("proxy")]
    [Authorize(Roles = "SystemAdmin,DepartmentManager,Supervisor")]
    public async Task<ActionResult<ActivityDto>> ProxyLog([FromBody] ProxyLogRequest req)
    {
        // Complete any previously Active entry for this staff member
        var existing = await db.StaffActivities
            .Where(a => a.StaffEmail == req.StaffEmail && a.Status == ActivityStatus.Active)
            .ToListAsync();
        foreach (var old in existing)
        {
            old.Status      = ActivityStatus.Completed;
            old.CompletedAt = DateTime.UtcNow;
            old.UpdatedAt   = DateTime.UtcNow;
        }

        var activity = new StaffActivity
        {
            StaffEmail          = req.StaffEmail,
            StaffName           = req.StaffName,
            ActivityDescription = req.ActivityDescription,
            Category            = req.Category,
            Location            = req.Location ?? "",
            Notes               = req.Notes,
            IsProxy             = true,
            LoggedByEmail       = CallerEmail,
            LoggedByName        = CallerName,
        };

        db.StaffActivities.Add(activity);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = activity.Id }, ToDto(activity));
    }

    // ── PATCH /api/v1/activities/{id}/status ──────────────────────────────
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<ActivityDto>> UpdateStatus(
        Guid id, [FromBody] UpdateActivityStatusRequest req)
    {
        var a = await db.StaffActivities.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();

        // Non-admin/supervisor can only update their own activities
        if (CallerRole is not ("SystemAdmin" or "DepartmentManager" or "Supervisor")
            && a.StaffEmail != CallerEmail)
            return Forbid();

        a.Status    = req.Status;
        a.UpdatedAt = DateTime.UtcNow;
        if (req.Notes is not null) a.Notes = req.Notes;
        if (req.Status == ActivityStatus.Completed)
            a.CompletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(ToDto(a));
    }

    // ── DELETE /api/v1/activities/{id} ────────────────────────────────────
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,DepartmentManager,Supervisor")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var a = await db.StaffActivities.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        db.StaffActivities.Remove(a);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
