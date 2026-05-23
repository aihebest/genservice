namespace GenService.API.Domain;

/// <summary>
/// Core request entity — tracks every service request raised in the platform.
/// </summary>
public class ServiceRequest
{
    public Guid    Id               { get; set; } = Guid.NewGuid();
    public string  TicketNumber     { get; set; } = "";       // REQ-2026-0001
    public string  Title            { get; set; } = "";
    public string  Description      { get; set; } = "";
    public string  Category         { get; set; } = "";       // see RequestCategory constants
    public bool    RequiresApproval { get; set; }
    public string  Status           { get; set; } = RequestStatus.Open;
    public string  Priority         { get; set; } = RequestPriority.Normal;
    public string  Location         { get; set; } = "";

    // ── Who raised it ────────────────────────────────────────────────
    public string RequestedByEmail { get; set; } = "";
    public string RequestedByName  { get; set; } = "";

    // ── Assignment ───────────────────────────────────────────────────
    public string? AssignedToEmail { get; set; }
    public string? AssignedToName  { get; set; }

    // ── Approval ─────────────────────────────────────────────────────
    public string?   ApprovedByEmail  { get; set; }
    public string?   ApprovedByName   { get; set; }
    public DateTime? ApprovedAt       { get; set; }
    public string?   RejectionReason  { get; set; }

    // ── Timestamps ───────────────────────────────────────────────────
    public DateTime  CreatedAt   { get; set; } = DateTime.UtcNow;
    public DateTime  UpdatedAt   { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public string? Notes { get; set; }
}

// ── Category constants ─────────────────────────────────────────────────────
public static class RequestCategory
{
    // No-approval categories
    public const string Maintenance        = "Maintenance";
    public const string FaultyAsset        = "FaultyAsset";
    public const string FacilityComplaint  = "FacilityComplaint";
    public const string OperationalSupport = "OperationalSupport";

    // Approval-required categories
    public const string Accommodation  = "Accommodation";
    public const string AssetDamage    = "AssetDamage";
    public const string WeekendAccess  = "WeekendAccess";
    public const string AfterHoursWork = "AfterHoursWork";
    public const string StoreItems     = "StoreItems";
    public const string Diesel         = "Diesel";
    public const string Other          = "Other";

    public static readonly HashSet<string> RequiresApproval = new()
    {
        Accommodation, AssetDamage, WeekendAccess,
        AfterHoursWork, StoreItems, Diesel
    };

    public static bool NeedsApproval(string category) =>
        RequiresApproval.Contains(category);
}

// ── Status constants ───────────────────────────────────────────────────────
public static class RequestStatus
{
    public const string Open            = "Open";
    public const string PendingApproval = "PendingApproval";
    public const string Approved        = "Approved";
    public const string Rejected        = "Rejected";
    public const string InProgress      = "InProgress";
    public const string Completed       = "Completed";
    public const string Cancelled       = "Cancelled";
}

// ── Priority constants ─────────────────────────────────────────────────────
public static class RequestPriority
{
    public const string Low    = "Low";
    public const string Normal = "Normal";
    public const string High   = "High";
    public const string Urgent = "Urgent";
}
