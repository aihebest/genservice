using GenService.API.Data;
using GenService.API.Domain;

namespace GenService.API.Services;

/// <summary>
/// Creates in-app notification records and optionally sends emails.
/// Used by all controllers to fire notifications on workflow events.
/// </summary>
public class NotificationService(
    GenServiceDbContext db,
    IEmailService       email,
    ILogger<NotificationService> log)
{
    // ── Core method ───────────────────────────────────────────────────────────
    public async Task CreateAsync(
        string  title,
        string  message,
        string  type,
        string  module,
        string? entityId    = null,
        string? refNumber   = null,
        string  targetRole  = NotificationTarget.Management,
        string? targetEmail = null)
    {
        db.AppNotifications.Add(new AppNotification
        {
            Title       = title,
            Message     = message,
            Type        = type,
            Module      = module,
            EntityId    = entityId,
            RefNumber   = refNumber,
            TargetRole  = targetRole,
            TargetEmail = targetEmail,
            IsRead      = false,
            CreatedAt   = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        log.LogInformation("🔔 Notification created: [{Type}] {Title}", type, title);
    }

    // ── Convenience wrappers ──────────────────────────────────────────────────

    public Task RequestSubmittedAsync(string refNumber, string entityId,
        string category, string requestedBy, string location) =>
        CreateAsync(
            title:      $"New request: {refNumber}",
            message:    $"{requestedBy} raised a {category} request at {location}.",
            type:       NotificationType.RequestSubmitted,
            module:     "Requests",
            entityId:   entityId,
            refNumber:  refNumber,
            targetRole: NotificationTarget.Management);

    public async Task RequestLineManagerApprovedAsync(string refNumber, string entityId,
        string approverName, string requesterEmail, string requesterName)
    {
        await CreateAsync(
            title:      $"{refNumber} — Line Manager Approved",
            message:    $"Your request {refNumber} was approved by {approverName} and is now pending General Service review.",
            type:       NotificationType.LineManagerApproved,
            module:     "Requests",
            entityId:   entityId,
            refNumber:  refNumber,
            targetRole: NotificationTarget.Requester,
            targetEmail:requesterEmail);

        await email.SendAsync(
            toEmail:  requesterEmail,
            toName:   requesterName,
            subject:  $"[GenService] {refNumber} — Line Manager Approved",
            htmlBody: EmailTemplates.Approved(requesterName, refNumber,
                $"approved by your Line Manager ({approverName})",
                "Your request is now awaiting final review by the General Service team."));
    }

    public async Task RequestLineManagerRejectedAsync(string refNumber, string entityId,
        string rejectorName, string reason, string requesterEmail, string requesterName)
    {
        await CreateAsync(
            title:      $"{refNumber} — Rejected by Line Manager",
            message:    $"Your request {refNumber} was rejected by {rejectorName}. Reason: {reason}",
            type:       NotificationType.LineManagerRejected,
            module:     "Requests",
            entityId:   entityId,
            refNumber:  refNumber,
            targetRole: NotificationTarget.Requester,
            targetEmail:requesterEmail);

        await email.SendAsync(
            toEmail:  requesterEmail,
            toName:   requesterName,
            subject:  $"[GenService] {refNumber} — Rejected",
            htmlBody: EmailTemplates.Rejected(requesterName, refNumber, rejectorName, reason));
    }

    public async Task RequestGsApprovedAsync(string refNumber, string entityId,
        string approverName, string requesterEmail, string requesterName)
    {
        await CreateAsync(
            title:      $"{refNumber} — Approved ✅",
            message:    $"Your request {refNumber} has been fully approved by {approverName} and is being processed.",
            type:       NotificationType.GsApproved,
            module:     "Requests",
            entityId:   entityId,
            refNumber:  refNumber,
            targetRole: NotificationTarget.Requester,
            targetEmail:requesterEmail);

        await email.SendAsync(
            toEmail:  requesterEmail,
            toName:   requesterName,
            subject:  $"[GenService] {refNumber} — Approved ✅",
            htmlBody: EmailTemplates.Approved(requesterName, refNumber,
                $"approved by {approverName} (General Service)",
                "Your request will now be assigned and actioned by the General Service team."));
    }

    public async Task RequestGsRejectedAsync(string refNumber, string entityId,
        string rejectorName, string reason, string requesterEmail, string requesterName)
    {
        await CreateAsync(
            title:      $"{refNumber} — Rejected by General Service",
            message:    $"Your request {refNumber} was rejected by General Service. Reason: {reason}",
            type:       NotificationType.GsRejected,
            module:     "Requests",
            entityId:   entityId,
            refNumber:  refNumber,
            targetRole: NotificationTarget.Requester,
            targetEmail:requesterEmail);

        await email.SendAsync(
            toEmail:  requesterEmail,
            toName:   requesterName,
            subject:  $"[GenService] {refNumber} — Rejected",
            htmlBody: EmailTemplates.Rejected(requesterName, refNumber, rejectorName, reason));
    }

    public Task RequestCompletedAsync(string refNumber, string entityId,
        string requesterEmail, string requesterName) =>
        CreateAsync(
            title:      $"{refNumber} — Completed ✅",
            message:    $"Your request {refNumber} has been completed by the General Service team.",
            type:       NotificationType.RequestCompleted,
            module:     "Requests",
            entityId:   entityId,
            refNumber:  refNumber,
            targetRole: NotificationTarget.Requester,
            targetEmail:requesterEmail);

    public Task MaintenancePendingAsync(string refNumber, string entityId,
        string module, string description, string requestedBy) =>
        CreateAsync(
            title:      $"New {module} request: {refNumber}",
            message:    $"{requestedBy}: {description}",
            type:       NotificationType.MaintenancePending,
            module:     module,
            entityId:   entityId,
            refNumber:  refNumber,
            targetRole: NotificationTarget.Management);
}
