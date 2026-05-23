using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/maintenance")]
[Authorize]
public class MaintenanceController(GenServiceDbContext db) : ControllerBase
{
    // ── Helpers ───────────────────────────────────────────────────────────────
    private string CallerEmail => User.FindFirst("email")?.Value
                               ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                               ?? "unknown@demo.local";
    private string CallerName  => User.FindFirst("name")?.Value
                               ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                               ?? "Unknown User";

    private static ScheduleDto ToDto(MaintenanceSchedule m) => new(
        m.Id, m.TaskName, m.Description, m.Category, m.Location,
        m.FrequencyLabel, m.FrequencyDays,
        m.NextDueAt, m.LastCompletedAt, m.IsOverdue,
        m.AssignedToEmail, m.AssignedToName,
        m.LastCompletedByEmail, m.LastCompletedByName, m.LastCompletionNotes,
        m.IsActive, m.CreatedAt, m.UpdatedAt
    );

    // ── GET /api/v1/maintenance ─────────────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<ScheduleListResponse>> List(
        [FromQuery] string? category,
        [FromQuery] bool?   overdueOnly,
        [FromQuery] bool?   activeOnly,
        [FromQuery] string? search,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20)
    {
        var q = db.MaintenanceSchedules.AsNoTracking();

        if (activeOnly ?? true)
            q = q.Where(m => m.IsActive);

        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(m => m.Category == category);

        if (overdueOnly == true)
            q = q.Where(m => m.NextDueAt < DateTime.UtcNow);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(m =>
                m.TaskName.ToLower().Contains(s) ||
                m.Location.ToLower().Contains(s) ||
                (m.Description != null && m.Description.ToLower().Contains(s)));
        }

        var total   = await q.CountAsync();
        var overdue = await q.CountAsync(m => m.NextDueAt < DateTime.UtcNow);
        var items   = await q
            .OrderBy(m => m.NextDueAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => ToDto(m))
            .ToListAsync();

        return Ok(new ScheduleListResponse(items, total, overdue, page, pageSize));
    }

    // ── GET /api/v1/maintenance/stats ───────────────────────────────────────
    [HttpGet("stats")]
    public async Task<ActionResult<MaintenanceStatsDto>> Stats()
    {
        var now     = DateTime.UtcNow;
        var soon    = now.AddDays(7);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var total     = await db.MaintenanceSchedules.CountAsync(m => m.IsActive);
        var overdue   = await db.MaintenanceSchedules.CountAsync(m => m.IsActive && m.NextDueAt < now);
        var dueSoon   = await db.MaintenanceSchedules.CountAsync(m => m.IsActive && m.NextDueAt >= now && m.NextDueAt <= soon);
        var completed = await db.MaintenanceSchedules.CountAsync(m => m.LastCompletedAt >= monthStart);
        var active    = total;

        return Ok(new MaintenanceStatsDto(total, overdue, dueSoon, completed, active));
    }

    // ── GET /api/v1/maintenance/{id} ────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ScheduleDto>> GetById(Guid id)
    {
        var m = await db.MaintenanceSchedules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return m is null ? NotFound() : Ok(ToDto(m));
    }

    // ── POST /api/v1/maintenance ────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "SystemAdmin,DepartmentManager,Supervisor")]
    public async Task<ActionResult<ScheduleDto>> Create([FromBody] CreateScheduleRequest req)
    {
        var schedule = new MaintenanceSchedule
        {
            TaskName        = req.TaskName,
            Description     = req.Description ?? "",
            Category        = req.Category,
            Location        = req.Location ?? "",
            FrequencyLabel  = req.FrequencyLabel,
            FrequencyDays   = req.FrequencyDays,
            NextDueAt       = req.NextDueAt,
            AssignedToEmail = req.AssignedToEmail,
            AssignedToName  = req.AssignedToName,
        };

        db.MaintenanceSchedules.Add(schedule);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = schedule.Id }, ToDto(schedule));
    }

    // ── PATCH /api/v1/maintenance/{id} ──────────────────────────────────────
    [HttpPatch("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,DepartmentManager,Supervisor")]
    public async Task<ActionResult<ScheduleDto>> Update(Guid id, [FromBody] UpdateScheduleRequest req)
    {
        var m = await db.MaintenanceSchedules.FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();

        if (req.TaskName      is not null) m.TaskName      = req.TaskName;
        if (req.Description   is not null) m.Description   = req.Description;
        if (req.Category      is not null) m.Category      = req.Category;
        if (req.Location      is not null) m.Location      = req.Location;
        if (req.FrequencyLabel is not null) m.FrequencyLabel = req.FrequencyLabel;
        if (req.FrequencyDays is not null) m.FrequencyDays = req.FrequencyDays.Value;
        if (req.NextDueAt     is not null) m.NextDueAt     = req.NextDueAt.Value;
        if (req.AssignedToEmail is not null) m.AssignedToEmail = req.AssignedToEmail;
        if (req.AssignedToName  is not null) m.AssignedToName  = req.AssignedToName;
        if (req.IsActive      is not null) m.IsActive      = req.IsActive.Value;
        m.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(ToDto(m));
    }

    // ── POST /api/v1/maintenance/{id}/complete ──────────────────────────────
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<ScheduleDto>> Complete(Guid id, [FromBody] CompleteScheduleRequest req)
    {
        var m = await db.MaintenanceSchedules.FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();

        m.LastCompletedAt      = DateTime.UtcNow;
        m.LastCompletedByEmail = req.CompletedByEmail;
        m.LastCompletedByName  = req.CompletedByName;
        m.LastCompletionNotes  = req.Notes;
        // Advance NextDueAt by the frequency
        m.NextDueAt  = DateTime.UtcNow.AddDays(m.FrequencyDays);
        m.UpdatedAt  = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(ToDto(m));
    }

    // ── DELETE /api/v1/maintenance/{id} ─────────────────────────────────────
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,DepartmentManager")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var m = await db.MaintenanceSchedules.FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        db.MaintenanceSchedules.Remove(m);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
