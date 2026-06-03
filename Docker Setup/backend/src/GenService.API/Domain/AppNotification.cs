namespace GenService.API.Domain;

/// <summary>
/// In-app notification record.
/// Created automatically when key workflow events occur (request submitted,
/// approved, rejected, completed, etc.).
/// </summary>
public class AppNotification
{
    public Guid     Id         { get; set; } = Guid.NewGuid();
    public string   Title      { get; set; } = "";
    public string   Message    { get; set; } = "";
    public string   Type       { get; set; } = "";    // see NotificationType constants
    public string   Module     { get; set; } = "";    // Requests | Equipment | Facility | Vehicle
    public string?  EntityId   { get; set; }          // guid of related entity
    public string?  RefNumber  { get; set; }          // REQ-2026-0001, E/26/001, etc.
    public bool     IsRead     { get; set; } = false;

    // ── Who should see it ─────────────────────────────────────────────────────
    public string   TargetRole  { get; set; } = "";   // Management | Requester | All
    public string?  TargetEmail { get; set; }         // specific user (optional)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class NotificationType
{
    public const string RequestSubmitted      = "RequestSubmitted";
    public const string LineManagerApproved   = "LineManagerApproved";
    public const string LineManagerRejected   = "LineManagerRejected";
    public const string GsApproved            = "GsApproved";
    public const string GsRejected            = "GsRejected";
    public const string RequestAssigned       = "RequestAssigned";
    public const string RequestCompleted      = "RequestCompleted";
    public const string MaintenancePending    = "MaintenancePending";
    public const string VehicleLongStanding   = "VehicleLongStanding";
}

public static class NotificationTarget
{
    public const string Management = "Management";   // DepartmentManager + Supervisor
    public const string Requester  = "Requester";    // the person who raised the request
    public const string All        = "All";
}
