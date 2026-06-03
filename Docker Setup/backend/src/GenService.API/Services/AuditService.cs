using GenService.API.Data;
using GenService.API.Domain;

namespace GenService.API.Services;

/// <summary>
/// Writes immutable audit entries. Inject into any controller that needs
/// to produce a tamper-evident history trail.
/// </summary>
public class AuditService(GenServiceDbContext db, ILogger<AuditService> log)
{
    public async Task LogAsync(
        string  entityType,
        string  entityId,
        string  refNumber,
        string  action,
        string  performedByEmail,
        string  performedByName,
        string? oldValue = null,
        string? newValue = null,
        string? details  = null)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            EntityType        = entityType,
            EntityId          = entityId,
            RefNumber         = refNumber,
            Action            = action,
            OldValue          = oldValue,
            NewValue          = newValue,
            Details           = details,
            PerformedByEmail  = performedByEmail,
            PerformedByName   = performedByName,
            Timestamp         = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        log.LogInformation("🔍 AUDIT [{EntityType}:{Ref}] {Action} by {User}",
            entityType, refNumber, action, performedByEmail);
    }

    // ── Convenience overloads ─────────────────────────────────────────────────

    public Task LogCreatedAsync(string entityType, string entityId, string refNumber,
        string by, string byName, string? details = null) =>
        LogAsync(entityType, entityId, refNumber, AuditAction.Created, by, byName,
            details: details);

    public Task LogStatusChangedAsync(string entityType, string entityId, string refNumber,
        string oldStatus, string newStatus, string by, string byName, string? reason = null) =>
        LogAsync(entityType, entityId, refNumber, AuditAction.StatusChanged, by, byName,
            oldValue: oldStatus, newValue: newStatus, details: reason);

    public Task LogAsync(string entityType, string entityId, string refNumber,
        string action, string by, string byName, string? details) =>
        LogAsync(entityType, entityId, refNumber, action, by, byName, details: details);
}
