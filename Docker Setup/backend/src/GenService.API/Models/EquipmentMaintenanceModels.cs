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
    double? RunningHours    = null,
    double? NextServiceHour = null
);

public record ApproveEquipmentRequest(string? Notes);
public record RejectEquipmentRequest(string Reason);

/// <summary>Record fault assessment / findings after inspection</summary>
public record EquipmentAssessmentRequest(
    string?  FaultIdentified,
    string?  ProposedSolution,
    string?  ResolutionType,       // Internal | Outsourced
    bool     PartsRequired,
    string?  PartsSource,          // StoreInventory | NewPurchase
    string?  ProcurementMethod,    // PO | CashAdvance
    decimal? SparesCostNaira
);

public record CompleteEquipmentRequest(
    string   WorkDone,
    string?  ActionedBy,
    decimal? SparesCostNaira = null,
    string?  Notes           = null
);

public record EquipmentHandoverRequest(
    string    HandedOverBy,
    DateTime? DateHandedOver = null
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
