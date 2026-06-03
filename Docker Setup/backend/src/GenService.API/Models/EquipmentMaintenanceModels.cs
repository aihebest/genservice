namespace GenService.API.Models;

// ── Inbound ───────────────────────────────────────────────────────────────────

public record CreateEquipmentMaintenanceRequest(
    string  AssetNo,
    string  AssetDescription,
    string  MaintenanceType,
    string  EndUser,
    string  Location,
    string  Description,
    string  Priority,
    double? RunningHours,
    double? NextServiceHour
);

public record ApproveEquipmentRequest(string? Notes);
public record RejectEquipmentRequest(string Reason);

public record CompleteEquipmentRequest(
    string  WorkDone,
    string? ActionedBy,
    string? Notes
);

public record EquipmentQuery(
    string? Status   = null,
    string? Type     = null,
    string? Search   = null,
    int     Page     = 1,
    int     PageSize = 20
);

// ── Outbound ──────────────────────────────────────────────────────────────────

public record EquipmentMaintenanceDto(
    Guid      Id,
    string    RequestNumber,
    string    AssetNo,
    string    AssetDescription,
    string    MaintenanceType,
    string    EndUser,
    string    Location,
    double?   RunningHours,
    double?   NextServiceHour,
    string    Description,
    string    Priority,
    string    Status,
    string    RequestedByEmail,
    string    RequestedByName,
    string?   ApprovedByEmail,
    string?   ApprovedByName,
    DateTime? ApprovedAt,
    string?   RejectionReason,
    string?   WorkDone,
    string?   ActionedBy,
    string?   Notes,
    DateTime? CompletedAt,
    DateTime  CreatedAt,
    DateTime  UpdatedAt,
    int       DaysOpen
);

public record EquipmentMaintenanceStatsDto(
    int Pending,
    int Approved,
    int Ongoing,
    int AwaitingSpares,
    int AwaitingFunds,
    int CompletedThisMonth,
    int Rejected
);

public record EquipmentMaintenanceListResponse(
    IEnumerable<EquipmentMaintenanceDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
