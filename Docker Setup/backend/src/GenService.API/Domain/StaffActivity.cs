namespace GenService.API.Domain;

/// <summary>
/// Real-time staff activity log — tracks what each team member is doing.
/// Supports proxy logging when a technician doesn't have device access.
/// </summary>
public class StaffActivity
{
    public Guid   Id                  { get; set; } = Guid.NewGuid();
    public string StaffEmail          { get; set; } = "";
    public string StaffName           { get; set; } = "";
    public string ActivityDescription { get; set; } = "";
    public string Location            { get; set; } = "";
    public string Category            { get; set; } = ActivityCategory.General;
    public string Status              { get; set; } = ActivityStatus.Active;
    public string? Notes              { get; set; }

    // ── Proxy logging support ─────────────────────────────────────────────
    public bool   IsProxy        { get; set; }       // true = logged by a supervisor on behalf of staff
    public string LoggedByEmail  { get; set; } = "";
    public string LoggedByName   { get; set; } = "";

    // ── Timestamps ────────────────────────────────────────────────────────
    public DateTime  StartedAt   { get; set; } = DateTime.UtcNow;
    public DateTime  UpdatedAt   { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public static class ActivityStatus
{
    public const string Active    = "Active";
    public const string Paused    = "Paused";
    public const string Completed = "Completed";
}

public static class ActivityCategory
{
    public const string Maintenance   = "Maintenance";
    public const string Repair        = "Repair";
    public const string Inspection    = "Inspection";
    public const string Cleaning      = "Cleaning";
    public const string Delivery      = "Delivery";
    public const string Installation  = "Installation";
    public const string GeneratorWork = "GeneratorWork";
    public const string Plumbing      = "Plumbing";
    public const string Electrical    = "Electrical";
    public const string General       = "General";
}
