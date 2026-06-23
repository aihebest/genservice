namespace GenService.API.Domain;

/// <summary>
/// Tracks facility repair and maintenance requests (Facility Repair & Maintenance Register).
/// Covers electrical works, plumbing, civil works, painting, tank washing, A/C, etc.
/// Reference format: F/26/001
/// </summary>
public class FacilityMaintenanceRequest
{
    public Guid   Id            { get; set; } = Guid.NewGuid();
    public string RequestNumber { get; set; } = "";   // F/26/001

    // ── What and where ────────────────────────────────────────────────────────
    public string  MaintenanceType { get; set; } = FacilityMaintenanceType.General;
    public string  Description     { get; set; } = "";    // problem reported by requestor
    public string  Location        { get; set; } = "";
    public string  EndUser         { get; set; } = "";    // PHC OFFICE, DR, WOJI, CHAIRMAN, etc.
    public string? RoomFlat        { get; set; }          // specific room, flat, or area

    // ── Request details ───────────────────────────────────────────────────────
    public string  Priority { get; set; } = RequestPriority.Normal;
    public string  Status   { get; set; } = MaintenanceRequestStatus.Pending;

    // ── Requester ─────────────────────────────────────────────────────────────
    public string  RequestedByEmail { get; set; } = "";
    public string  RequestedByName  { get; set; } = "";

    // ── Approval ──────────────────────────────────────────────────────────────
    public string?   ApprovedByEmail { get; set; }
    public string?   ApprovedByName  { get; set; }
    public DateTime? ApprovedAt      { get; set; }
    public string?   RejectionReason { get; set; }

    // ── Fault assessment ──────────────────────────────────────────────────────
    public string?  FaultIdentified  { get; set; }   // actual fault/issue identified on site
    public string?  ProposedSolution { get; set; }   // recommended repair action
    public string?  ResolutionType   { get; set; }   // Internal | Outsourced

    // ── Parts & procurement ───────────────────────────────────────────────────
    public bool     PartsRequired     { get; set; } = false;
    public string?  PartsSource       { get; set; }   // StoreInventory | NewPurchase
    public string?  ProcurementMethod { get; set; }   // PO | CashAdvance
    public decimal? SparesCostNaira   { get; set; }

    // ── Work completion ───────────────────────────────────────────────────────
    public string?   WorkDone    { get; set; }   // full description of work done
    public string?   ActionedBy  { get; set; }   // staff or contractor who did the work
    public DateTime? CompletedAt { get; set; }

    // ── Handover ─────────────────────────────────────────────────────────────
    public bool      HandoverConfirmed { get; set; } = false;
    public DateTime? DateHandedOver    { get; set; }
    public string?   HandedOverBy      { get; set; }

    public string?   Notes     { get; set; }
    public DateTime  CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime  UpdatedAt { get; set; } = DateTime.UtcNow;
}

// ── Facility maintenance types ────────────────────────────────────────────────
public static class FacilityMaintenanceType
{
    public const string Electrical    = "Electrical";
    public const string Plumbing      = "Plumbing";
    public const string CivilWorks    = "CivilWorks";
    public const string Painting      = "Painting";
    public const string TankWashing   = "TankWashing";
    public const string FireSafety    = "FireSafety";
    public const string Fumigation    = "Fumigation";
    public const string Carpentry     = "Carpentry";
    public const string ACService     = "ACService";       // A/C service/repair in rooms
    public const string SepticTank    = "SepticTank";      // septic tank evacuation
    public const string Glasswork     = "Glasswork";       // glass replacement
    public const string General       = "General";
}
