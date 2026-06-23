namespace GenService.API.Models;

// ── Inbound ───────────────────────────────────────────────────────────────────

public record CreateVehicleMaintenanceRequest(
    string  VehicleRegNo,
    string  VehicleType,
    string  MaintenanceType,
    string  Description,
    string  Priority,
    string  CurrentLocation,
    string? OdometerReading = null
);

public record ApproveVehicleMaintenanceRequest(string? Notes);
public record RejectVehicleMaintenanceRequest(string Reason);

public record SendToWorkshopRequest(
    string    WorkshopName,
    string?   WorkshopLocation,
    DateTime? DateDeliveredToWorkshop = null
);

/// <summary>Record fault assessment after workshop inspection</summary>
public record VehicleAssessmentRequest(
    string?  FaultIdentified,
    string?  ProposedSolution,
    string?  ResolutionType,       // Internal | Outsourced
    bool     PartsRequired,
    string?  PartsSource,          // StoreInventory | NewPurchase
    string?  ProcurementMethod,    // PO | CashAdvance
    string?  PartsSuppliedBy,
    decimal? SparesCostNaira
);

public record CompleteVehicleMaintenanceRequest(
    string?  WorkDone,
    string?  ActionedBy,
    decimal? SparesCostNaira = null,
    string?  Notes           = null
);

public record VehicleHandoverRequest(
    string    HandedOverBy,
    DateTime? DateHandedOver = null
);

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
    string?   OdometerReading,
    string    RequestedByEmail,
    string    RequestedByName,
    string?   ApprovedByEmail,
    string?   ApprovedByName,
    DateTime? ApprovedAt,
    string?   RejectionReason,
    // Workshop
    string?   WorkshopName,
    string?   WorkshopLocation,
    DateTime? DateDeliveredToWorkshop,
    DateTime? SentToWorkshopAt,
    // Assessment
    string?   FaultIdentified,
    string?   ProposedSolution,
    string?   ResolutionType,
    // Parts
    bool      PartsRequired,
    string?   PartsSource,
    string?   ProcurementMethod,
    string?   PartsSuppliedBy,
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
    int       DaysOpen,
    int?      DaysInWorkshop
);

public record VehicleMaintenanceStatsDto(
    int Pending,
    int Approved,
    int InWorkshop,
    int AwaitingParts,
    int AwaitingFunds,
    int CompletedThisMonth,
    int Rejected,
    int LongStanding
);

public record VehicleMaintenanceListResponse(
    IEnumerable<VehicleMaintenanceDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
