namespace GenService.API.Domain;

/// <summary>
/// Tracks ad-hoc facility repair and maintenance requests.
/// Covers electrical works, plumbing, civil works, painting, tank washing, etc.
/// Reference format: F/26/001
/// </summary>
public class FacilityMaintenanceRequest
{
    public Guid   Id            { get; set; } = Guid.NewGuid();
    public string RequestNumber { get; set; } = "";   // F/26/001

    // ── What and where ────────────────────────────────────────────────────────
    public string  MaintenanceType { get; set; } = FacilityMaintenanceType.General;
    public string  Description     { get; set; } = "";
    public string  Location        { get; set; } = "";
    public string  EndUser         { get; set; } = "";    // PHC Office, DR, Woji, Chairman, etc.
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

    // ── Work completion ───────────────────────────────────────────────────────
    public string?   WorkDone    { get; set; }   // description of work done
    public string?   ActionedBy  { get; set; }   // Woji Store / Third Party / staff name
    public DateTime? CompletedAt { get; set; }

    public string?   Notes     { get; set; }
    public DateTime  CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime  UpdatedAt { get; set; } = DateTime.UtcNow;
}

// ── Facility maintenance types ────────────────────────────────────────────────
public static class FacilityMaintenanceType
{
    public const string Electrical   = "Electrical";
    public const string Plumbing     = "Plumbing";
    public const string CivilWorks   = "CivilWorks";
    public const string Painting     = "Painting";
    public const string TankWashing  = "TankWashing";
    public const string FireSafety   = "FireSafety";
    public const string Fumigation   = "Fumigation";
    public const string Carpentry    = "Carpentry";
    public const string General      = "General";
}
