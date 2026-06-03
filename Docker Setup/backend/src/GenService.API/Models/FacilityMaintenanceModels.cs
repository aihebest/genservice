namespace GenService.API.Models;

// ── Inbound ───────────────────────────────────────────────────────────────────

public record CreateFacilityMaintenanceRequest(
    string  MaintenanceType,
    string  Description,
    string  Location,
    string  EndUser,
    string? RoomFlat,
    string  Priority
);

public record ApproveFacilityRequest(string? Notes);
public record RejectFacilityRequest(string Reason);

public record CompleteFacilityRequest(
    string  WorkDone,
    string? ActionedBy,
    string? Notes
);

public record FacilityQuery(
    string? Status   = null,
    string? Type     = null,
    string? Search   = null,
    int     Page     = 1,
    int     PageSize = 20
);

// ── Outbound ──────────────────────────────────────────────────────────────────

public record FacilityMaintenanceDto(
    Guid      Id,
    string    RequestNumber,
    string    MaintenanceType,
    string    Description,
    string    Location,
    string    EndUser,
    string?   RoomFlat,
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

public record FacilityMaintenanceStatsDto(
    int Pending,
    int Approved,
    int Ongoing,
    int AwaitingSpares,
    int AwaitingFunds,
    int CompletedThisMonth,
    int Rejected
);

public record FacilityMaintenanceListResponse(
    IEnumerable<FacilityMaintenanceDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
