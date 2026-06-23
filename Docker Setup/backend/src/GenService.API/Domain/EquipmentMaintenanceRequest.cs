namespace GenService.API.Domain;

/// <summary>
/// Tracks equipment repair and maintenance requests (Equipment Repair & Maintenance Register).
/// Covers generators, air conditioners, UPS systems, pumps, and other plant equipment.
/// Reference format: E/26/001
/// </summary>
public class EquipmentMaintenanceRequest
{
    public Guid   Id            { get; set; } = Guid.NewGuid();
    public string RequestNumber { get; set; } = "";   // E/26/001

    // ── Asset info ────────────────────────────────────────────────────────────
    public string  AssetNo          { get; set; } = "";   // e.g. 02135, 03188
    public string  AssetDescription { get; set; } = "";   // e.g. PHC OFFICE CAT 350KVA GENERATOR
    public string  MaintenanceType  { get; set; } = EquipmentMaintenanceType.GeneratorService;
    public string  EndUser          { get; set; } = "";   // DR, PHC OFFICE, WOJI, CHAIRMAN, etc.
    public string  Location         { get; set; } = "";

    // ── Generator-specific tracking ───────────────────────────────────────────
    public double? RunningHours     { get; set; }   // current cumulative hour reading
    public double? NextServiceHour  { get; set; }   // next service due at

    // ── Request details ───────────────────────────────────────────────────────
    public string  Description { get; set; } = "";           // problem reported by requestor
    public string  Priority    { get; set; } = RequestPriority.Normal;
    public string  Status      { get; set; } = MaintenanceRequestStatus.Pending;

    // ── Requester ─────────────────────────────────────────────────────────────
    public string  RequestedByEmail { get; set; } = "";
    public string  RequestedByName  { get; set; } = "";

    // ── Approval ──────────────────────────────────────────────────────────────
    public string?   ApprovedByEmail { get; set; }
    public string?   ApprovedByName  { get; set; }
    public DateTime? ApprovedAt      { get; set; }
    public string?   RejectionReason { get; set; }

    // ── Fault assessment ─────────────────────────────────────────────────────
    public string?  FaultIdentified   { get; set; }   // actual fault found on inspection
    public string?  ProposedSolution  { get; set; }   // recommended repair/action
    public string?  ResolutionType    { get; set; }   // Internal | Outsourced

    // ── Parts & procurement ───────────────────────────────────────────────────
    public bool     PartsRequired     { get; set; } = false;
    public string?  PartsSource       { get; set; }   // StoreInventory | NewPurchase
    public string?  ProcurementMethod { get; set; }   // PO | CashAdvance
    public decimal? SparesCostNaira   { get; set; }

    // ── Work completion ───────────────────────────────────────────────────────
    public string?   WorkDone      { get; set; }   // description of work done
    public string?   ActionedBy    { get; set; }   // staff name or third party who did the work
    public DateTime? CompletedAt   { get; set; }

    // ── Handover (equipment returned to operational) ──────────────────────────
    public bool      HandoverConfirmed { get; set; } = false;
    public DateTime? DateHandedOver    { get; set; }
    public string?   HandedOverBy      { get; set; }

    public string?   Notes     { get; set; }
    public DateTime  CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime  UpdatedAt { get; set; } = DateTime.UtcNow;
}

// ── Shared maintenance request status (used by Equipment & Facility) ──────────
public static class MaintenanceRequestStatus
{
    public const string Pending         = "Pending";          // awaiting approval
    public const string Approved        = "Approved";         // approved, assigned
    public const string Ongoing         = "Ongoing";          // work in progress
    public const string AwaitingSpares  = "AwaitingSpares";   // waiting for parts
    public const string AwaitingFunds   = "AwaitingFunds";    // waiting for budget
    public const string Completed       = "Completed";
    public const string Rejected        = "Rejected";
}

// ── Equipment maintenance types ───────────────────────────────────────────────
public static class EquipmentMaintenanceType
{
    public const string GeneratorService  = "GeneratorService";    // 250-hr service, fuel/water filter
    public const string GeneratorRepair   = "GeneratorRepair";     // breakdown/repair
    public const string ACService         = "ACService";           // air conditioning service
    public const string ACRepair          = "ACRepair";            // air conditioning repair
    public const string UPSMaintenance    = "UPSMaintenance";      // UPS/inverter
    public const string PumpService       = "PumpService";         // borehole pump, water pump
    public const string Electrical        = "Electrical";          // electrical equipment
    public const string Other             = "Other";
}
