using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController(GenServiceDbContext db) : ControllerBase
{
    // ── GET /api/v1/dashboard/summary ─────────────────────────────────────────
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummary>> Summary()
    {
        var now        = DateTime.UtcNow;
        var todayStart = now.Date;
        var soon       = now.AddDays(7);

        // ── KPIs — sequential (EF Core DbContext is not thread-safe) ─────────
        var openRequests      = await db.ServiceRequests.CountAsync(r => r.Status == RequestStatus.Open);
        var pendingApproval   = await db.ServiceRequests.CountAsync(r => r.Status == RequestStatus.PendingApproval);
        var inProgress        = await db.ServiceRequests.CountAsync(r => r.Status == RequestStatus.InProgress);
        var completedToday    = await db.ServiceRequests.CountAsync(r =>
                                    r.Status == RequestStatus.Completed && r.CompletedAt >= todayStart);
        var totalRequests     = await db.ServiceRequests.CountAsync();
        var staffActive       = await db.StaffActivities.CountAsync(a => a.Status == ActivityStatus.Active);
        var maintOverdue      = await db.MaintenanceSchedules.CountAsync(m => m.IsActive && m.NextDueAt < now);
        var maintDueSoon      = await db.MaintenanceSchedules.CountAsync(m => m.IsActive && m.NextDueAt >= now && m.NextDueAt <= soon);

        // ── Panels ────────────────────────────────────────────────────────────

        // Recent requests (newest 6)
        var recentRequests = await db.ServiceRequests
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Take(6)
            .Select(r => new DashboardRequestItem(
                r.Id.ToString(), r.TicketNumber, r.Title, r.Category,
                r.Status, r.Priority, r.RequestedByName, r.AssignedToName, r.CreatedAt))
            .ToListAsync();

        // Pending approvals (oldest first so nothing is buried)
        var pendingApprovals = await db.ServiceRequests
            .AsNoTracking()
            .Where(r => r.Status == RequestStatus.PendingApproval)
            .OrderBy(r => r.CreatedAt)
            .Take(5)
            .Select(r => new DashboardRequestItem(
                r.Id.ToString(), r.TicketNumber, r.Title, r.Category,
                r.Status, r.Priority, r.RequestedByName, r.AssignedToName, r.CreatedAt))
            .ToListAsync();

        // Active staff
        var activeStaff = await db.StaffActivities
            .AsNoTracking()
            .Where(a => a.Status == ActivityStatus.Active)
            .OrderBy(a => a.StaffName)
            .Select(a => new DashboardActivityItem(
                a.Id.ToString(), a.StaffName, a.ActivityDescription,
                a.Category, a.Location, a.IsProxy, a.LoggedByName, a.StartedAt))
            .ToListAsync();

        // Upcoming maintenance — overdue first, then soonest due (max 6)
        var upcomingMaintenance = await db.MaintenanceSchedules
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.NextDueAt)
            .Take(6)
            .Select(m => new DashboardMaintenanceItem(
                m.Id.ToString(), m.TaskName, m.Category, m.Location,
                m.NextDueAt < now,
                (int)Math.Round((m.NextDueAt - now).TotalDays),
                m.FrequencyLabel, m.AssignedToName, m.NextDueAt))
            .ToListAsync();

        return Ok(new DashboardSummary(
            openRequests,
            pendingApproval,
            inProgress,
            completedToday,
            totalRequests,
            staffActive,
            maintOverdue,
            maintDueSoon,
            recentRequests,
            pendingApprovals,
            activeStaff,
            upcomingMaintenance
        ));
    }
}
