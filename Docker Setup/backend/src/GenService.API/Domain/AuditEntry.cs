namespace GenService.API.Domain;

/// <summary>
/// Immutable audit record. One row per significant action across the platform.
/// Never updated — only inserted. Provides a full tamper-evident history trail.
/// </summary>
public class AuditEntry
{
    public Guid     Id               { get; set; } = Guid.NewGuid();

    // ── What was affected ─────────────────────────────────────────────────────
    public string   EntityType       { get; set; } = ""; // Request | Equipment | Facility | Vehicle | Diesel | Generator
    public string   EntityId         { get; set; } = ""; // GUID of related entity
    public string   RefNumber        { get; set; } = ""; // REQ-2026-0001, E/26/001, etc.

    // ── What happened ─────────────────────────────────────────────────────────
    public string   Action           { get; set; } = ""; // see AuditAction constants
    public string?  OldValue         { get; set; }       // previous status / field value
    public string?  NewValue         { get; set; }       // new status / field value
    public string?  Details          { get; set; }       // free-text context (reason, notes, etc.)

    // ── Who did it ────────────────────────────────────────────────────────────
    public string   PerformedByEmail { get; set; } = "";
    public string   PerformedByName  { get; set; } = "";

    public DateTime Timestamp        { get; set; } = DateTime.UtcNow;
}

// ── Action constants ──────────────────────────────────────────────────────────
public static class AuditAction
{
    public const string Created          = "Created";
    public const string StatusChanged    = "StatusChanged";
    public const string Approved         = "Approved";
    public const string Rejected         = "Rejected";
    public const string LineManagerApproved = "LineManagerApproved";
    public const string LineManagerRejected = "LineManagerRejected";
    public const string Assigned         = "Assigned";
    public const string Reassigned       = "Reassigned";
    public const string Completed        = "Completed";
    public const string Cancelled        = "Cancelled";
    public const string Dispatched       = "Dispatched";   // vehicle to workshop
    public const string ProgressLogged   = "ProgressLogged";
    public const string NoteAdded        = "NoteAdded";
}
