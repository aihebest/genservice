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

    // ── Maintenance Reminder wrappers ─────────────────────────────────────────

    public async Task MaintenanceDueSoonAsync(
        Guid    scheduleId,
        string  taskName,
        string  location,
        string  category,
        DateTime dueAt,
        int      daysLeft,
        string?  assignedToEmail,
        string?  assignedToName)
    {
        var dueStr  = dueAt.ToString("d MMM yyyy");
        var dayStr  = daysLeft == 1 ? "1 day" : $"{daysLeft} days";
        var urgency = daysLeft <= 1 ? NotificationType.MaintenanceDueUrgent : NotificationType.MaintenanceDueSoon;
        var title   = daysLeft <= 1
            ? $"⚠️ Due TOMORROW: {taskName}"
            : $"📅 Due in {dayStr}: {taskName}";

        // In-app — notify management
        await CreateAsync(
            title:      title,
            message:    $"{taskName} at {location} is due on {dueStr}.",
            type:       urgency,
            module:     "Maintenance",
            entityId:   scheduleId.ToString(),
            targetRole: NotificationTarget.Management);

        // If assigned staff, send email reminder
        if (!string.IsNullOrWhiteSpace(assignedToEmail) && !string.IsNullOrWhiteSpace(assignedToName))
        {
            await email.SendAsync(
                toEmail:  assignedToEmail,
                toName:   assignedToName,
                subject:  $"[GenService] Maintenance Due Soon: {taskName}",
                htmlBody: EmailTemplates.MaintenanceDueSoon(
                    assignedToName, taskName, location, category, dueStr, dayStr));
        }
    }

    public async Task MaintenanceEscalateToSupervisorAsync(
        Guid    scheduleId,
        string  taskName,
        string  location,
        string  category,
        DateTime dueAt,
        int      daysOverdue,
        string?  supervisorEmail,
        string?  supervisorName)
    {
        var dueStr     = dueAt.ToString("d MMM yyyy");
        var overdueStr = daysOverdue == 1 ? "1 day" : $"{daysOverdue} days";

        await CreateAsync(
            title:      $"🚨 Overdue ({overdueStr}): {taskName}",
            message:    $"Escalated to Supervisor — {taskName} at {location} was due {dueStr}.",
            type:       NotificationType.MaintenanceEscalationSupervisor,
            module:     "Maintenance",
            entityId:   scheduleId.ToString(),
            targetRole: NotificationTarget.Management);

        if (!string.IsNullOrWhiteSpace(supervisorEmail) && !string.IsNullOrWhiteSpace(supervisorName))
        {
            await email.SendAsync(
                toEmail:  supervisorEmail,
                toName:   supervisorName,
                subject:  $"[GenService] ESCALATION — Maintenance Overdue: {taskName}",
                htmlBody: EmailTemplates.MaintenanceEscalation(
                    supervisorName, taskName, location, category, dueStr, overdueStr, "Supervisor"));
        }
    }

    public async Task MaintenanceEscalateToManagerAsync(
        Guid    scheduleId,
        string  taskName,
        string  location,
        string  category,
        DateTime dueAt,
        int      daysOverdue,
        string?  managerEmail,
        string?  managerName)
    {
        var dueStr     = dueAt.ToString("d MMM yyyy");
        var overdueStr = daysOverdue == 1 ? "1 day" : $"{daysOverdue} days";

        await CreateAsync(
            title:      $"🆘 Manager Escalation ({overdueStr} overdue): {taskName}",
            message:    $"Escalated to Department Manager — {taskName} at {location} was due {dueStr}.",
            type:       NotificationType.MaintenanceEscalationManager,
            module:     "Maintenance",
            entityId:   scheduleId.ToString(),
            targetRole: NotificationTarget.Management);

        if (!string.IsNullOrWhiteSpace(managerEmail) && !string.IsNullOrWhiteSpace(managerName))
        {
            await email.SendAsync(
                toEmail:  managerEmail,
                toName:   managerName,
                subject:  $"[GenService] MANAGER ESCALATION — Maintenance Critical: {taskName}",
                htmlBody: EmailTemplates.MaintenanceEscalation(
                    managerName, taskName, location, category, dueStr, overdueStr, "Department Manager"));
        }
    }
}
