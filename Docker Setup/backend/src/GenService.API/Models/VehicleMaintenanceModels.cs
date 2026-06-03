namespace GenService.API.Models;

// ── Inbound ───────────────────────────────────────────────────────────────────

public record CreateVehicleMaintenanceRequest(
    string VehicleRegNo,
    string VehicleType,
    string MaintenanceType,
    string Description,
    string Priority,
    string CurrentLocation
);

public record ApproveVehicleMaintenanceRequest(string? Notes);
public record RejectVehicleMaintenanceRequest(string Reason);

public record SendToWorkshopRequest(
    string  WorkshopName,
    string? WorkshopLocation
);

public record CompleteVehicleMaintenanceRequest(string? Notes);

public record VehicleMaintenanceQuery(
    string? Status   = null,
    string? Type     = null,
    string? Search   = null,
    int     Page     = 1,
    int     PageSize = 20
);

// ── Outbound ──────────────────────────────────────────────────────────────────

public record VehicleMaintenanceDto(
    Guid      Id,
    string    RequestNumber,
    string    VehicleRegNo,
    string    VehicleType,
    string    MaintenanceType,
    string    Description,
    string    Priority,
    string    Status,
    string    CurrentLocation,
    string    RequestedByEmail,
    string    RequestedByName,
    string?   ApprovedByEmail,
    string?   ApprovedByName,
    DateTime? ApprovedAt,
    string?   RejectionReason,
    string?   WorkshopName,
    string?   WorkshopLocation,
    DateTime? SentToWorkshopAt,
    DateTime? CompletedAt,
    string?   Notes,
    DateTime  CreatedAt,
    DateTime  UpdatedAt,
    int       DaysOpen,          // days since request was raised
    int?      DaysInWorkshop     // days since sent to workshop (null if not dispatched)
);

public record VehicleMaintenanceStatsDto(
    int Pending,
    int Approved,
    int InWorkshop,
    int CompletedThisMonth,
    int Rejected,
    int LongStanding   // in workshop > 7 days
);

public record VehicleMaintenanceListResponse(
    IEnumerable<VehicleMaintenanceDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
