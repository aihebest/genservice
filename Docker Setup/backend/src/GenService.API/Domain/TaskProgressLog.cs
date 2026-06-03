namespace GenService.API.Domain;

/// <summary>
/// Daily technician progress update against any assigned task.
/// Module-agnostic: works for Service Requests, Equipment, Facility, and Vehicle maintenance.
/// </summary>
public class TaskProgressLog
{
    public Guid     Id        { get; set; } = Guid.NewGuid();

    // ── Which task this update belongs to ─────────────────────────────────────
    public string Module    { get; set; } = "";   // Requests | Equipment | Facility | Vehicle
    public string EntityId  { get; set; } = "";   // GUID of related entity
    public string RefNumber { get; set; } = "";   // REQ-2026-0001, E/26/001, etc.
    public string TaskTitle { get; set; } = "";   // request title / asset description (for display)

    // ── Progress update content ───────────────────────────────────────────────
    public DateTime LogDate            { get; set; } = DateTime.UtcNow.Date;
    public string   ActivityPerformed  { get; set; } = "";
    public string   ProgressStatus     { get; set; } = Domain.ProgressStatus.WorkInProgress;
    public string?  MaterialsRequired  { get; set; }
    public string?  NextAction         { get; set; }

    // ── Who submitted this update ─────────────────────────────────────────────
    public string LoggedByEmail { get; set; } = "";
    public string LoggedByName  { get; set; } = "";
    public bool   IsProxy       { get; set; } = false;    // true = supervisor logged on behalf
    public string? ProxyForName { get; set; }             // technician's name when IsProxy

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ── Progress status constants ─────────────────────────────────────────────────
public static class ProgressStatus
{
    public const string WorkCompleted      = "WorkCompleted";
    public const string WorkInProgress     = "WorkInProgress";
    public const string AwaitingMaterials  = "AwaitingMaterials";
    public const string AwaitingVendor     = "AwaitingVendor";
    public const string AwaitingApproval   = "AwaitingApproval";
    public const string PendingProcurement = "PendingProcurement";
}
