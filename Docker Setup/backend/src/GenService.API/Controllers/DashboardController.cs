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
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // ── Request KPIs ─────────────────────────────────────────────────────
        var openRequests    = await db.ServiceRequests.CountAsync(r => r.Status == RequestStatus.Open);
        var pendingApproval = await db.ServiceRequests.CountAsync(r => r.Status == RequestStatus.PendingApproval);
        var inProgress      = await db.ServiceRequests.CountAsync(r => r.Status == RequestStatus.InProgress);
        var completedToday  = await db.ServiceRequests.CountAsync(r =>
                                  r.Status == RequestStatus.Completed && r.CompletedAt >= todayStart);
        var totalRequests   = await db.ServiceRequests.CountAsync();

        // ── Staff KPI ────────────────────────────────────────────────────────
        var staffActive = await db.StaffActivities.CountAsync(a => a.Status == ActivityStatus.Active);

        // ── Maintenance KPIs ─────────────────────────────────────────────────
        var maintOverdue      = await db.MaintenanceSchedules.CountAsync(m => m.IsActive && m.NextDueAt < now);
        var maintDueSoon      = await db.MaintenanceSchedules.CountAsync(m => m.IsActive && m.NextDueAt >= now && m.NextDueAt <= soon);
        var maintEscalations  = await db.MaintenanceSchedules.CountAsync(m => m.IsActive && m.EscalationLevel > 0);

        // ── Store KPIs ───────────────────────────────────────────────────────
        var storeReqsPending  = await db.StoreRequisitions.CountAsync(r => r.Status == "Pending");
        var storeLowStock     = await db.StoreItems.CountAsync(i => i.IsActive && i.QuantityInStock <= i.ReorderLevel && i.ReorderLevel > 0);

        // ── Diesel / Fuel KPIs ────────────────────────────────────────────────
        var dieselReqsPending = await db.DieselRequisitions.CountAsync(r => r.Status == DieselRequisitionStatus.Pending);

        var dieselDispensedMtd = await db.DieselRequisitions
            .Where(r => r.Status == DieselRequisitionStatus.Dispensed
                     && r.DispensedAt >= monthStart
                     && r.QuantityDispensedLitres.HasValue)
            .SumAsync(r => (double?)r.QuantityDispensedLitres) ?? 0.0;

        var latestTankLevel = await db.DieselTankReadings
            .OrderByDescending(t => t.ReadingDate)
            .Select(t => (double?)t.TankLevelLitres)
            .FirstOrDefaultAsync();

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
            .OrderByDescending(m => m.EscalationLevel)  // escalated tasks first
            .ThenBy(m => m.NextDueAt)
            .Take(6)
            .Select(m => new DashboardMaintenanceItem(
                m.Id.ToString(), m.TaskName, m.Category, m.Location,
                m.NextDueAt < now,
                (int)Math.Round((m.NextDueAt - now).TotalDays),
                m.FrequencyLabel, m.AssignedToName, m.NextDueAt,
                m.EscalationLevel))
            .ToListAsync();

        // Low stock items (at or below reorder level) — worst first
        var lowStockItems = await db.StoreItems
            .AsNoTracking()
            .Where(i => i.IsActive && i.QuantityInStock <= i.ReorderLevel && i.ReorderLevel > 0)
            .OrderBy(i => i.QuantityInStock)
            .Take(8)
            .Select(i => new DashboardStoreItem(
                i.Id.ToString(), i.ItemCode, i.Name, i.Category,
                i.QuantityInStock, i.ReorderLevel, i.Unit))
            .ToListAsync();

        // Recent diesel requisitions (newest 5)
        var recentDieselReqs = await db.DieselRequisitions
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .Select(r => new DashboardDieselReqItem(
                r.Id.ToString(), r.RequisitionNumber, r.EquipmentType,
                r.Purpose, r.QuantityRequestedLitres,
                r.QuantityDispensedLitres, r.Status,
                r.RequestedByName, r.CreatedAt))
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
            maintEscalations,
            storeReqsPending,
            storeLowStock,
            dieselReqsPending,
            dieselDispensedMtd,
            latestTankLevel,
            recentRequests,
            pendingApprovals,
            activeStaff,
            upcomingMaintenance,
            lowStockItems,
            recentDieselReqs
        ));
    }
}
