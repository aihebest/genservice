namespace GenService.API.Domain;

/// <summary>
/// Tracks vehicle repair and maintenance requests (Vehicle Repair & Maintenance Register).
/// Mirrors the physical MRSF register format used by Desicon Group.
/// Reference format: V/26/001
/// </summary>
public class VehicleMaintenanceRequest
{
    public Guid   Id            { get; set; } = Guid.NewGuid();
    public string RequestNumber { get; set; } = "";   // V/26/001

    // ── Vehicle info ─────────────────────────────────────────────────────────
    public string  VehicleRegNo      { get; set; } = "";   // e.g. PHC 185 AM
    public string  VehicleType       { get; set; } = "";   // NISSAN PICKUP, TOYOTA HILUX, etc.
    public string  MaintenanceType   { get; set; } = VehicleMaintenanceType.RoutineService;
    public string  CurrentLocation   { get; set; } = "";   // where vehicle currently is
    public string? OdometerReading   { get; set; }         // odometer at time of request (km/miles)

    // ── Request details ───────────────────────────────────────────────────────
    public string Description { get; set; } = "";           // problem reported by requestor/user
    public string Priority    { get; set; } = RequestPriority.Normal;
    public string Status      { get; set; } = VehicleMaintenanceStatus.Pending;

    // ── Requester (Logistics / Driver) ────────────────────────────────────────
    public string RequestedByEmail { get; set; } = "";
    public string RequestedByName  { get; set; } = "";

    // ── Approval ──────────────────────────────────────────────────────────────
    public string?   ApprovedByEmail { get; set; }
    public string?   ApprovedByName  { get; set; }
    public DateTime? ApprovedAt      { get; set; }
    public string?   RejectionReason { get; set; }

    // ── Workshop dispatch ─────────────────────────────────────────────────────
    public string?   WorkshopName         { get; set; }
    public string?   WorkshopLocation     { get; set; }
    public DateTime? DateDeliveredToWorkshop { get; set; }  // physical delivery date
    public DateTime? SentToWorkshopAt     { get; set; }     // system dispatch timestamp

    // ── Fault assessment (workshop findings) ─────────────────────────────────
    public string?  FaultIdentified   { get; set; }   // actual fault found by workshop
    public string?  ProposedSolution  { get; set; }   // recommended repair action
    public string?  ResolutionType    { get; set; }   // Internal | Outsourced

    // ── Parts & procurement ───────────────────────────────────────────────────
    public bool     PartsRequired       { get; set; } = false;
    public string?  PartsSource         { get; set; }   // StoreInventory | NewPurchase
    public string?  ProcurementMethod   { get; set; }   // PO | CashAdvance
    public string?  PartsSuppliedBy     { get; set; }   // e.g. "Woji Store", "Third Party"
    public decimal? SparesCostNaira     { get; set; }

    // ── Work done ─────────────────────────────────────────────────────────────
    public string?   WorkDone     { get; set; }   // description of work completed
    public string?   ActionedBy   { get; set; }   // GS staff who actioned the repair

    // ── Handover (vehicle returned to user) ───────────────────────────────────
    public bool      HandoverConfirmed { get; set; } = false;
    public DateTime? DateHandedOver    { get; set; }
    public string?   HandedOverBy      { get; set; }   // GS personnel responsible for handover

    // ── Closure ───────────────────────────────────────────────────────────────
    public DateTime? CompletedAt { get; set; }
    public string?   Notes       { get; set; }

    // ── Timestamps ────────────────────────────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// ── Status constants ──────────────────────────────────────────────────────────
public static class VehicleMaintenanceStatus
{
    public const string Pending         = "Pending";          // submitted, awaiting GS approval
    public const string Approved        = "Approved";         // approved, pending workshop dispatch
    public const string InWorkshop      = "InWorkshop";       // vehicle is with mechanic
    public const string AwaitingParts   = "AwaitingParts";    // waiting for spare parts
    public const string AwaitingFunds   = "AwaitingFunds";    // waiting for budget/funds
    public const string Completed       = "Completed";        // repairs done, vehicle returned
    public const string Rejected        = "Rejected";
}

// ── Maintenance type constants (per MRSF register) ───────────────────────────
public static class VehicleMaintenanceType
{
    public const string RoutineService = "RoutineService";   // Routine Service & Maintenance
    public const string MinorRepair    = "MinorRepair";      // Minor Repair & Maintenance
    public const string MajorRepair    = "MajorRepair";      // Major Repair & Maintenance
}

// ── Resolution type constants ─────────────────────────────────────────────────
public static class VehicleResolutionType
{
    public const string Internal   = "Internal";    // resolved in-house
    public const string Outsourced = "Outsourced";  // sent to external vendor
}

// ── Parts source constants ────────────────────────────────────────────────────
public static class PartsSource
{
    public const string StoreInventory = "StoreInventory";  // from existing store
    public const string NewPurchase    = "NewPurchase";     // purchased new
}

// ── Procurement method constants ──────────────────────────────────────────────
public static class ProcurementMethod
{
    public const string PurchaseOrder = "PurchaseOrder";   // formal PO
    public const string CashAdvance   = "CashAdvance";     // cash advance
}
