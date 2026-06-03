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
    public async Task<IActionResult> List([FromQuery] bool unreadOnly = false, [FromQuery] int take = 20)
    {
        var all = await db.AppNotifications
            .AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .Take(200)  // load latest 200 and filter in memory (small table)
            .ToListAsync();

        var visible = all.Where(CanSee);
        if (unreadOnly) visible = visible.Where(n => !n.IsRead);

        var items = visible.Take(take).Select(n => new
        {
            n.Id, n.Title, n.Message, n.Type, n.Module,
            n.EntityId, n.RefNumber, n.IsRead, n.CreatedAt,
        });

        var unreadCount = all.Where(CanSee).Count(n => !n.IsRead);

        return Ok(new { items, unreadCount });
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
}
