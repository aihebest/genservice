namespace GenService.API.Domain;

/// <summary>
/// Tracks ad-hoc equipment repair and maintenance requests.
/// Covers generators, air conditioners, UPS systems, pumps, and other plant equipment.
/// Reference format: E/26/001
/// </summary>
public class EquipmentMaintenanceRequest
{
    public Guid   Id            { get; set; } = Guid.NewGuid();
    public string RequestNumber { get; set; } = "";   // E/26/001

    // ── Asset info ────────────────────────────────────────────────────────────
    public string  AssetNo          { get; set; } = "";   // e.g. 6660003188
    public string  AssetDescription { get; set; } = "";   // e.g. DR CUMMINS 275KVA GENERATOR
    public string  MaintenanceType  { get; set; } = EquipmentMaintenanceType.GeneratorService;
    public string  EndUser          { get; set; } = "";   // DR, PHC Office, Woji, etc.
    public string  Location         { get; set; } = "";

    // ── Generator-specific tracking ───────────────────────────────────────────
    public double? RunningHours     { get; set; }   // current reading
    public double? NextServiceHour  { get; set; }   // next service due at

    // ── Request details ───────────────────────────────────────────────────────
    public string  Description { get; set; } = "";
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

    // ── Work completion ───────────────────────────────────────────────────────
    public string?   WorkDone      { get; set; }   // description of work done
    public string?   ActionedBy    { get; set; }   // Woji Store / Third Party / staff name
    public DateTime? CompletedAt   { get; set; }

    public string?   Notes     { get; set; }
    public DateTime  CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime  UpdatedAt { get; set; } = DateTime.UtcNow;
}

// ── Shared maintenance request status (used by Equipment & Facility) ──────────
public static class MaintenanceRequestStatus
{
    public const string Pending        = "Pending";         // awaiting approval
    public const string Approved       = "Approved";        // approved, assigned
    public const string Ongoing        = "Ongoing";         // work in progress
    public const string AwaitingSpares = "AwaitingSpares";  // waiting for parts
    public const string AwaitingFunds  = "AwaitingFunds";   // waiting for budget
    public const string Completed      = "Completed";
    public const string Rejected       = "Rejected";
}

// ── Equipment maintenance types ───────────────────────────────────────────────
public static class EquipmentMaintenanceType
{
    public const string GeneratorService = "GeneratorService";
    public const string ACService        = "ACService";          // Air conditioning
    public const string UPSMaintenance   = "UPSMaintenance";
    public const string PumpService      = "PumpService";
    public const string Electrical       = "Electrical";
    public const string Other            = "Other";
}
