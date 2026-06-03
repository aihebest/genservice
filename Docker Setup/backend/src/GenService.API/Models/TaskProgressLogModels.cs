namespace GenService.API.Models;

// ── Inbound ───────────────────────────────────────────────────────────────────

public record CreateTaskProgressLogRequest(
    string  Module,
    string  EntityId,
    string  RefNumber,
    string  TaskTitle,
    string  ActivityPerformed,
    string  ProgressStatus,
    string? MaterialsRequired,
    string? NextAction,
    // Proxy support
    bool    IsProxy       = false,
    string? ProxyForName  = null
);

// ── Outbound ──────────────────────────────────────────────────────────────────

public record TaskProgressLogDto(
    Guid      Id,
    string    Module,
    string    EntityId,
    string    RefNumber,
    string    TaskTitle,
    DateTime  LogDate,
    string    ActivityPerformed,
    string    ProgressStatus,
    string?   MaterialsRequired,
    string?   NextAction,
    string    LoggedByEmail,
    string    LoggedByName,
    bool      IsProxy,
    string?   ProxyForName,
    DateTime  CreatedAt
);

public record TechnicianSummary(
    string  Email,
    string  Name,
    int     TotalAssigned,
    int     InProgress,
    int     Completed,
    int     Pending,
    int     TodayLogs,
    int     WeekLogs,
    int     MonthLogs,
    int     AwaitingMaterials,
    int     AwaitingVendor
);
