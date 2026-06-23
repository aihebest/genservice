using GenService.API.Data;
using GenService.API.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController(GenServiceDbContext db) : ControllerBase
{
    private string CallerRole  => User.FindFirstValue(ClaimTypes.Role)  ?? "";
    private string CallerEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";

    private bool CanSee(AppNotification n)
    {
        // "All" notifications are visible to everyone
        if (n.TargetRole == NotificationTarget.All) return true;

        // Management target → visible to DepartmentManager, Supervisor, SystemAdmin
        if (n.TargetRole == NotificationTarget.Management &&
            CallerRole is "DepartmentManager" or "Supervisor" or "SystemAdmin")
            return true;

        // Requester target → visible if the specific email matches
        if (n.TargetRole == NotificationTarget.Requester &&
            n.TargetEmail == CallerEmail)
            return true;

        return false;
    }

    // ── GET /api/v1/notifications ────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool    unreadOnly = false,
        [FromQuery] int     take       = 20,
        [FromQuery] int     page       = 1,
        [FromQuery] string? module     = null,
        [FromQuery] string? type       = null)
    {
        var all = await db.AppNotifications
            .AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .Take(500)   // load latest 500 and filter in memory
            .ToListAsync();

        var visible = all.Where(CanSee).AsEnumerable();
        if (unreadOnly)                                  visible = visible.Where(n => !n.IsRead);
        if (!string.IsNullOrWhiteSpace(module))          visible = visible.Where(n => n.Module == module);
        if (!string.IsNullOrWhiteSpace(type))            visible = visible.Where(n => n.Type   == type);

        var visibleList  = visible.ToList();
        var total        = visibleList.Count;
        var unreadCount  = all.Where(CanSee).Count(n => !n.IsRead);

        var items = visibleList
            .Skip((page - 1) * take)
            .Take(take)
            .Select(n => new
            {
                n.Id, n.Title, n.Message, n.Type, n.Module,
                n.EntityId, n.RefNumber, n.IsRead, n.CreatedAt,
            });

        return Ok(new { items, unreadCount, total, page, take });
    }

    // ── PATCH /api/v1/notifications/{id}/read ───────────────────────────────
    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var n = await db.AppNotifications.FindAsync(id);
        if (n is null) return NotFound();
        n.IsRead = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── PATCH /api/v1/notifications/{id}/unread ─────────────────────────────
    [HttpPatch("{id:guid}/unread")]
    public async Task<IActionResult> MarkUnread(Guid id)
    {
        var n = await db.AppNotifications.FindAsync(id);
        if (n is null) return NotFound();
        n.IsRead = false;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── PATCH /api/v1/notifications/read-all ────────────────────────────────
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var all = await db.AppNotifications
            .Where(n => !n.IsRead)
            .ToListAsync();

        var mine = all.Where(CanSee).ToList();
        mine.ForEach(n => n.IsRead = true);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── DELETE /api/v1/notifications/{id} ───────────────────────────────────
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var n = await db.AppNotifications.FindAsync(id);
        if (n is null) return NotFound();
        db.AppNotifications.Remove(n);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── DELETE /api/v1/notifications/clear-read ─────────────────────────────
    /// <summary>Deletes all read notifications visible to the caller.</summary>
    [HttpDelete("clear-read")]
    public async Task<IActionResult> ClearRead()
    {
        var all = await db.AppNotifications
            .Where(n => n.IsRead)
            .ToListAsync();

        var mine = all.Where(CanSee).ToList();
        db.AppNotifications.RemoveRange(mine);
        await db.SaveChangesAsync();
        return Ok(new { deleted = mine.Count });
    }

    // ── GET /api/v1/notifications/modules ───────────────────────────────────
    /// <summary>Returns the distinct module names visible to the caller (for filter UI).</summary>
    [HttpGet("modules")]
    public async Task<IActionResult> Modules()
    {
        var all = await db.AppNotifications
            .AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .Take(500)
            .ToListAsync();

        var modules = all.Where(CanSee)
            .Select(n => n.Module)
            .Distinct()
            .OrderBy(m => m)
            .ToList();

        return Ok(modules);
    }
}
