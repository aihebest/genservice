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
    DateTime NextDueAt,
    int      EscalationLevel  // 0=None, 1=Supervisor, 2=Manager
);

public record DashboardStoreItem(
    string  Id,
    string  ItemCode,
    string  Name,
    string  Category,
    double  QuantityInStock,
    double  ReorderLevel,
    string  Unit
);

public record DashboardDieselReqItem(
    string    Id,
    string    RequisitionNumber,
    string    EquipmentType,
    string    Purpose,
    double    QuantityRequestedLitres,
    double?   QuantityDispensedLitres,
    string    Status,
    string    RequestedByName,
    DateTime  CreatedAt
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
    int MaintenanceDueSoon,       // next 7 days
    int MaintenanceEscalations,   // tasks at EscalationLevel > 0

    // ── Store KPIs
    int StoreReqsPending,
    int StoreLowStockCount,

    // ── Diesel / Fuel KPIs
    int     DieselReqsPending,
    double  DieselDispensedThisMonthLitres,
    double? DieselTankLevelLitres,         // latest tank reading (null if no reading logged yet)

    // ── Panels
    IReadOnlyList<DashboardRequestItem>     RecentRequests,           // last 6
    IReadOnlyList<DashboardRequestItem>     PendingApprovals,         // up to 5
    IReadOnlyList<DashboardActivityItem>    ActiveStaff,              // all active
    IReadOnlyList<DashboardMaintenanceItem> UpcomingMaintenance,      // next 6 (overdue first)
    IReadOnlyList<DashboardStoreItem>       LowStockItems,            // items at or below reorder level
    IReadOnlyList<DashboardDieselReqItem>   RecentDieselRequisitions  // last 5
);
