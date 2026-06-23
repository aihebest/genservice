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

/// <summary>Record fault assessment after site inspection</summary>
public record FacilityAssessmentRequest(
    string?  FaultIdentified,
    string?  ProposedSolution,
    string?  ResolutionType,       // Internal | Outsourced
    bool     PartsRequired,
    string?  PartsSource,          // StoreInventory | NewPurchase
    string?  ProcurementMethod,    // PO | CashAdvance
    decimal? SparesCostNaira
);

public record CompleteFacilityRequest(
    string   WorkDone,
    string?  ActionedBy,
    decimal? SparesCostNaira = null,
    string?  Notes           = null
);

public record FacilityHandoverRequest(
    string    HandedOverBy,
    DateTime? DateHandedOver = null
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
    // Assessment
    string?   FaultIdentified,
    string?   ProposedSolution,
    string?   ResolutionType,
    // Parts
    bool      PartsRequired,
    string?   PartsSource,
    string?   ProcurementMethod,
    decimal?  SparesCostNaira,
    // Completion
    string?   WorkDone,
    string?   ActionedBy,
    DateTime? CompletedAt,
    // Handover
    bool      HandoverConfirmed,
    DateTime? DateHandedOver,
    string?   HandedOverBy,
    string?   Notes,
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
