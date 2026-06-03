using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/task-logs")]
[Authorize]
public class TaskProgressLogController(
    GenServiceDbContext db,
    ILogger<TaskProgressLogController> logger) : ControllerBase
{
    private string CallerEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";
    private string CallerName  => User.FindFirstValue(ClaimTypes.Name)  ?? "";
    private string CallerRole  => User.FindFirstValue(ClaimTypes.Role)  ?? "";

    private static TaskProgressLogDto ToDto(TaskProgressLog l) => new(
        l.Id, l.Module, l.EntityId, l.RefNumber, l.TaskTitle,
        l.LogDate, l.ActivityPerformed, l.ProgressStatus,
        l.MaterialsRequired, l.NextAction,
        l.LoggedByEmail, l.LoggedByName,
        l.IsProxy, l.ProxyForName,
        l.CreatedAt
    );

    // ── GET /api/v1/task-logs?module=Requests&entityId=xxx ───────────────────
    /// <summary>Get all progress logs for a specific task.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskProgressLogDto>>> List(
        [FromQuery] string module,
        [FromQuery] string entityId)
    {
        var items = await db.TaskProgressLogs
            .AsNoTracking()
            .Where(l => l.Module == module && l.EntityId == entityId)
            .OrderByDescending(l => l.LogDate)
            .ThenByDescending(l => l.CreatedAt)
            .ToListAsync();

        return Ok(items.Select(ToDto));
    }

    // ── GET /api/v1/task-logs/mine ───────────────────────────────────────────
    /// <summary>Get all logs submitted by or on behalf of the current user.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<TaskProgressLogDto>>> Mine(
        [FromQuery] int days = 30)
    {
        var from = DateTime.UtcNow.AddDays(-days);
        var items = await db.TaskProgressLogs
            .AsNoTracking()
            .Where(l => l.LoggedByEmail == CallerEmail && l.CreatedAt >= from)
            .OrderByDescending(l => l.LogDate)
            .ToListAsync();

        return Ok(items.Select(ToDto));
    }

    // ── POST /api/v1/task-logs ───────────────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<TaskProgressLogDto>> Create(
        [FromBody] CreateTaskProgressLogRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ActivityPerformed))
            return BadRequest(new { message = "Activity description is required." });

        // Proxy restriction: only supervisors / managers can log on behalf of others
        if (req.IsProxy && CallerRole is not ("DepartmentManager" or "Supervisor" or "SystemAdmin"))
            return Forbid();

        var log = new TaskProgressLog
        {
            Module           = req.Module,
            EntityId         = req.EntityId,
            RefNumber        = req.RefNumber,
            TaskTitle        = req.TaskTitle,
            LogDate          = DateTime.UtcNow.Date,
            ActivityPerformed= req.ActivityPerformed.Trim(),
            ProgressStatus   = req.ProgressStatus,
            MaterialsRequired= req.MaterialsRequired?.Trim(),
            NextAction       = req.NextAction?.Trim(),
            LoggedByEmail    = CallerEmail,
            LoggedByName     = CallerName,
            IsProxy          = req.IsProxy,
            ProxyForName     = req.IsProxy ? req.ProxyForName : null,
            CreatedAt        = DateTime.UtcNow,
        };

        db.TaskProgressLogs.Add(log);
        await db.SaveChangesAsync();

        logger.LogInformation("Progress log for {Ref} ({Module}) by {User}",
            req.RefNumber, req.Module, CallerEmail);

        return CreatedAtAction(nameof(List),
            new { module = log.Module, entityId = log.EntityId },
            ToDto(log));
    }

    // ── GET /api/v1/task-logs/performance ────────────────────────────────────
    /// <summary>Per-technician performance summary for the management dashboard.</summary>
    [HttpGet("performance")]
    public async Task<ActionResult<IEnumerable<TechnicianSummary>>> Performance()
    {
        var now        = DateTime.UtcNow;
        var todayStart = now.Date;
        var weekStart  = todayStart.AddDays(-(int)todayStart.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1);

        // ── Gather assignments from ServiceRequests ───────────────────────────
        var requests = await db.ServiceRequests
            .AsNoTracking()
            .Where(r => r.AssignedToEmail != null)
            .ToListAsync();

        // ── Gather progress logs ──────────────────────────────────────────────
        var logs = await db.TaskProgressLogs
            .AsNoTracking()
            .ToListAsync();

        // ── Build per-technician summaries ────────────────────────────────────
        var emails = requests
            .Select(r => r.AssignedToEmail!)
            .Concat(logs.Select(l => l.LoggedByEmail))
            .Distinct()
            .ToList();

        // Map email → name from requests or logs
        var nameMap = requests
            .Where(r => r.AssignedToEmail != null && r.AssignedToName != null)
            .GroupBy(r => r.AssignedToEmail!)
            .ToDictionary(g => g.Key, g => g.First().AssignedToName ?? g.Key);

        foreach (var l in logs.Where(l => !nameMap.ContainsKey(l.LoggedByEmail)))
            nameMap[l.LoggedByEmail] = l.LoggedByName;

        var summaries = emails.Select(email =>
        {
            var myRequests = requests.Where(r => r.AssignedToEmail == email).ToList();
            var myLogs     = logs.Where(l => l.LoggedByEmail == email).ToList();

            return new TechnicianSummary(
                Email:             email,
                Name:              nameMap.GetValueOrDefault(email, email),
                TotalAssigned:     myRequests.Count,
                InProgress:        myRequests.Count(r => r.Status == RequestStatus.InProgress),
                Completed:         myRequests.Count(r => r.Status == RequestStatus.Completed),
                Pending:           myRequests.Count(r => r.Status is RequestStatus.Open
                                               or RequestStatus.Approved),
                TodayLogs:         myLogs.Count(l => l.LogDate >= todayStart),
                WeekLogs:          myLogs.Count(l => l.LogDate  >= weekStart),
                MonthLogs:         myLogs.Count(l => l.LogDate  >= monthStart),
                AwaitingMaterials: myLogs.Count(l => l.ProgressStatus == ProgressStatus.AwaitingMaterials),
                AwaitingVendor:    myLogs.Count(l => l.ProgressStatus == ProgressStatus.AwaitingVendor)
            );
        }).OrderByDescending(s => s.TotalAssigned).ToList();

        return Ok(summaries);
    }
}
