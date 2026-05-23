namespace GenService.API.Models;

// ── Slim projections for dashboard panels ─────────────────────────────────────

public record DashboardRequestItem(
    string   Id,
    string   TicketNumber,
    string   Title,
    string   Category,
    string   Status,
    string   Priority,
    string   RequestedByName,
    string?  AssignedToName,
    DateTime CreatedAt
);

public record DashboardActivityItem(
    string   Id,
    string   StaffName,
    string   ActivityDescription,
    string   Category,
    string   Location,
    bool     IsProxy,
    string   LoggedByName,
    DateTime StartedAt
);

public record DashboardMaintenanceItem(
    string   Id,
    string   TaskName,
    string   Category,
    string   Location,
    bool     IsOverdue,
    int      DaysUntilDue,    // negative = overdue
    string   FrequencyLabel,
    string?  AssignedToName,
    DateTime NextDueAt
);

// ── Top-level summary returned by GET /api/v1/dashboard/summary ──────────────

public record DashboardSummary(
    // ── Request KPIs
    int RequestsOpen,
    int RequestsPendingApproval,
    int RequestsInProgress,
    int RequestsCompletedToday,
    int RequestsTotal,

    // ── Staff KPIs
    int StaffActiveNow,

    // ── Maintenance KPIs
    int MaintenanceOverdue,
    int MaintenanceDueSoon,    // next 7 days

    // ── Panels
    IReadOnlyList<DashboardRequestItem>     RecentRequests,      // last 6
    IReadOnlyList<DashboardRequestItem>     PendingApprovals,    // up to 5
    IReadOnlyList<DashboardActivityItem>    ActiveStaff,         // all active
    IReadOnlyList<DashboardMaintenanceItem> UpcomingMaintenance  // next 6 (overdue first)
);
