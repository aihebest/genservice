namespace GenService.API.Domain;

/// <summary>
/// Tracks ad-hoc vehicle repair and maintenance requests raised by Logistics.
/// General Service oversees approval, workshop dispatch, and closure.
/// </summary>
public class VehicleMaintenanceRequest
{
    public Guid   Id            { get; set; } = Guid.NewGuid();
    public string RequestNumber { get; set; } = "";   // VM-2026-0001

    // ── Vehicle info ─────────────────────────────────────────────────────────
    public string VehicleRegNo     { get; set; } = "";  // e.g. LAG-342-TE
    public string VehicleType      { get; set; } = "";  // Toyota Hilux, Coaster Bus, etc.
    public string MaintenanceType  { get; set; } = VehicleMaintenanceType.Repair;

    // ── Request details ───────────────────────────────────────────────────────
    public string Description     { get; set; } = "";
    public string Priority        { get; set; } = RequestPriority.Normal;
    public string Status          { get; set; } = VehicleMaintenanceStatus.Pending;
    public string CurrentLocation { get; set; } = "";   // where vehicle currently is

    // ── Requester (Logistics / Driver) ────────────────────────────────────────
    public string RequestedByEmail { get; set; } = "";
    public string RequestedByName  { get; set; } = "";

    // ── Approval ──────────────────────────────────────────────────────────────
    public string?   ApprovedByEmail { get; set; }
    public string?   ApprovedByName  { get; set; }
    public DateTime? ApprovedAt      { get; set; }
    public string?   RejectionReason { get; set; }

    // ── Workshop dispatch ─────────────────────────────────────────────────────
    public string?   WorkshopName     { get; set; }
    public string?   WorkshopLocation { get; set; }
    public DateTime? SentToWorkshopAt { get; set; }

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
    public const string Pending    = "Pending";      // submitted, awaiting GS approval
    public const string Approved   = "Approved";     // approved, pending workshop dispatch
    public const string InWorkshop = "InWorkshop";   // vehicle is with mechanic
    public const string Completed  = "Completed";    // repairs done, vehicle returned
    public const string Rejected   = "Rejected";
}

// ── Maintenance type constants ────────────────────────────────────────────────
public static class VehicleMaintenanceType
{
    public const string Servicing   = "Servicing";
    public const string Repair      = "Repair";
    public const string Inspection  = "Inspection";
    public const string Bodywork    = "Bodywork";
    public const string TyreChange  = "TyreChange";
    public const string Battery     = "Battery";
    public const string Other       = "Other";
}
