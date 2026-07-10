using GenService.API.Data;
using GenService.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace GenService.API.Services;

/// <summary>
/// Hosted background service that runs every hour and:
/// 1. Sends 7-day advance "due soon" warnings to assigned staff + management.
/// 2. Sends 1-day urgent warnings.
/// 3. Escalates overdue tasks (1+ days) to the Supervisor (EscalationLevel 1).
/// 4. Escalates further (3+ days) to the Department Manager (EscalationLevel 2).
///
/// All actions are idempotent — tracked via EscalationLevel, LastReminderSentAt,
/// and LastEscalationSentAt to prevent duplicate notifications.
/// </summary>
public class MaintenanceReminderService(
    IServiceScopeFactory scopeFactory,
    ILogger<MaintenanceReminderService> log)
    : BackgroundService
{
    // ── Timing constants ──────────────────────────────────────────────────────
    /// <summary>How often the service checks for due/overdue tasks.</summary>
    private static readonly TimeSpan CheckInterval    = TimeSpan.FromHours(1);

    /// <summary>Minimum gap between two "due soon" reminders for the same task.</summary>
    private static readonly TimeSpan ReminderCooldown = TimeSpan.FromHours(23);

    /// <summary>Minimum gap between two escalation emails for the same task.</summary>
    private static readonly TimeSpan EscalationCooldown = TimeSpan.FromHours(23);

    // ── Trigger windows ───────────────────────────────────────────────────────
    private const int DueSoonDays   = 7;   // send 7-day warning
    private const int DueUrgentDays = 1;   // send 1-day warning (inside due-soon window)
    private const int EscalateLevel1AfterDaysOverdue = 1;   // → Supervisor
    private const int EscalateLevel2AfterDaysOverdue = 3;   // → Manager

    // ── BackgroundService entry point ─────────────────────────────────────────
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Delay 90 seconds on startup to let the app fully initialize
        await Task.Delay(TimeSpan.FromSeconds(90), ct);

        log.LogInformation("🔔 MaintenanceReminderService started — checking every {h}h.", CheckInterval.TotalHours);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunCheckAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                log.LogError(ex, "❌ MaintenanceReminderService check failed.");
            }

            await Task.Delay(CheckInterval, ct);
        }

        log.LogInformation("🛑 MaintenanceReminderService stopped.");
    }

    // ── Core check logic ──────────────────────────────────────────────────────
    private async Task RunCheckAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db   = scope.ServiceProvider.GetRequiredService<GenServiceDbContext>();
        var notif = scope.ServiceProvider.GetRequiredService<NotificationService>();

        var now = DateTime.UtcNow;

        // Load all active schedules in one query
        var schedules = await db.MaintenanceSchedules
            .Where(m => m.IsActive)
            .ToListAsync(ct);

        // Look up one Supervisor and one Manager from AppUsers for escalation emails
        var supervisor = await db.AppUsers
            .Where(u => u.IsActive && u.Role == "Supervisor")
            .OrderBy(u => u.FullName)
            .FirstOrDefaultAsync(ct);

        var manager = await db.AppUsers
            .Where(u => u.IsActive && u.Role == "DepartmentManager")
            .OrderBy(u => u.FullName)
            .FirstOrDefaultAsync(ct);

        int reminders = 0, escalations = 0;

        foreach (var schedule in schedules)
        {
            var daysUntilDue = (schedule.NextDueAt - now).TotalDays;
            var daysOverdue  = (now - schedule.NextDueAt).TotalDays;

            if (daysUntilDue > 0)
            {
                // ── Task is NOT yet overdue ────────────────────────────────────
                if (daysUntilDue <= DueSoonDays)
                {
                    // Send "due soon" reminder if cooldown has passed
                    bool cooldownExpired = schedule.LastReminderSentAt is null
                        || (now - schedule.LastReminderSentAt.Value) >= ReminderCooldown;

                    if (cooldownExpired)
                    {
                        var daysLeft = (int)Math.Ceiling(daysUntilDue);
                        await notif.MaintenanceDueSoonAsync(
                            schedule.Id,
                            schedule.TaskName,
                            schedule.Location,
                            schedule.Category,
                            schedule.NextDueAt,
                            daysLeft,
                            schedule.AssignedToEmail,
                            schedule.AssignedToName);

                        schedule.LastReminderSentAt = now;
                        schedule.UpdatedAt          = now;
                        reminders++;

                        log.LogInformation(
                            "📅 Reminder sent: '{Task}' due in {Days}d (ID {Id})",
                            schedule.TaskName, daysLeft, schedule.Id);
                    }
                }
            }
            else
            {
                // ── Task IS overdue ────────────────────────────────────────────
                var overdueDays = (int)Math.Floor(daysOverdue);

                // Level 1 escalation — Supervisor (1+ days overdue)
                if (overdueDays >= EscalateLevel1AfterDaysOverdue && schedule.EscalationLevel < 1)
                {
                    bool cooldownExpired = schedule.LastEscalationSentAt is null
                        || (now - schedule.LastEscalationSentAt.Value) >= EscalationCooldown;

                    if (cooldownExpired)
                    {
                        await notif.MaintenanceEscalateToSupervisorAsync(
                            schedule.Id,
                            schedule.TaskName,
                            schedule.Location,
                            schedule.Category,
                            schedule.NextDueAt,
                            overdueDays,
                            supervisor?.Email,
                            supervisor?.FullName);

                        schedule.EscalationLevel      = 1;
                        schedule.LastEscalationSentAt = now;
                        schedule.UpdatedAt            = now;
                        escalations++;

                        log.LogWarning(
                            "🚨 Escalation L1 → Supervisor: '{Task}' {Days}d overdue (ID {Id})",
                            schedule.TaskName, overdueDays, schedule.Id);
                    }
                }

                // Level 2 escalation — Manager (3+ days overdue)
                if (overdueDays >= EscalateLevel2AfterDaysOverdue && schedule.EscalationLevel < 2)
                {
                    bool cooldownExpired = schedule.LastEscalationSentAt is null
                        || (now - schedule.LastEscalationSentAt.Value) >= EscalationCooldown;

                    if (cooldownExpired)
                    {
                        await notif.MaintenanceEscalateToManagerAsync(
                            schedule.Id,
                            schedule.TaskName,
                            schedule.Location,
                            schedule.Category,
                            schedule.NextDueAt,
                            overdueDays,
                            manager?.Email,
                            manager?.FullName);

                        schedule.EscalationLevel      = 2;
                        schedule.LastEscalationSentAt = now;
                        schedule.UpdatedAt            = now;
                        escalations++;

                        log.LogWarning(
                            "🆘 Escalation L2 → Manager: '{Task}' {Days}d overdue (ID {Id})",
                            schedule.TaskName, overdueDays, schedule.Id);
                    }
                }

                // Also resend the escalation reminder if still at level 1 and cooldown expired
                // (daily re-notification so overdue items don't silently drop off)
                if (schedule.EscalationLevel == 1
                    && overdueDays >= EscalateLevel1AfterDaysOverdue
                    && overdueDays < EscalateLevel2AfterDaysOverdue)
                {
                    bool cooldownExpired = schedule.LastEscalationSentAt.HasValue
                        && (now - schedule.LastEscalationSentAt.Value) >= EscalationCooldown;

                    if (cooldownExpired)
                    {
                        await notif.MaintenanceEscalateToSupervisorAsync(
                            schedule.Id, schedule.TaskName, schedule.Location, schedule.Category,
                            schedule.NextDueAt, overdueDays, supervisor?.Email, supervisor?.FullName);
                        schedule.LastEscalationSentAt = now;
                        schedule.UpdatedAt            = now;
                    }
                }

                if (schedule.EscalationLevel == 2)
                {
                    bool cooldownExpired = schedule.LastEscalationSentAt.HasValue
                        && (now - schedule.LastEscalationSentAt.Value) >= EscalationCooldown;

                    if (cooldownExpired)
                    {
                        await notif.MaintenanceEscalateToManagerAsync(
                            schedule.Id, schedule.TaskName, schedule.Location, schedule.Category,
                            schedule.NextDueAt, overdueDays, manager?.Email, manager?.FullName);
                        schedule.LastEscalationSentAt = now;
                        schedule.UpdatedAt            = now;
                    }
                }
            }
        }

        // Persist all changes in one round-trip
        if (schedules.Any(s => s.UpdatedAt >= now.AddSeconds(-5)))
        {
            await db.SaveChangesAsync(ct);
        }

        if (reminders + escalations > 0)
        {
            log.LogInformation(
                "🔔 Reminder run complete — {R} reminders, {E} escalations sent.",
                reminders, escalations);
        }

        // ── Obinna's modules: expiry reminders ────────────────────────────────
        await RunDstvRemindersAsync(db, notif, now, ct);
        await RunVehicleDocumentRemindersAsync(db, notif, now, ct);
    }

    // ── DStv subscription renewal reminders (7 / 3 / 1 days) ───────────────────
    private static readonly int[] DstvMilestones = [7, 3, 1];

    private async Task RunDstvRemindersAsync(
        GenServiceDbContext db, NotificationService notif, DateTime now, CancellationToken ct)
    {
        var subs    = await db.DstvSubscriptions.ToListAsync(ct);
        var changed = false;

        foreach (var s in subs)
        {
            var days = (int)Math.Ceiling((s.ExpiryDate.Date - now.Date).TotalDays);

            if (days < 0)
            {
                s.Status = DstvStatus.Expired;
                if (!s.ExpiredNotified)
                {
                    await notif.CreateAsync(
                        title:      $"📺 DStv expired: {s.Location}",
                        message:    $"DStv decoder {s.DecoderNumber} at {s.Location} expired on {s.ExpiryDate:d MMM yyyy}. Renew to restore service.",
                        type:       NotificationType.SubscriptionExpired,
                        module:     "DStv",
                        entityId:   s.Id.ToString(),
                        refNumber:  s.DecoderNumber,
                        targetRole: NotificationTarget.Management);
                    s.ExpiredNotified = true;
                    s.UpdatedAt = now;
                    changed = true;
                }
                continue;
            }

            s.Status = days <= 7 ? DstvStatus.ExpiringSoon : DstvStatus.Active;

            // Fire once per milestone as we cross 7 → 3 → 1 days.
            var milestone = DstvMilestones.Where(m => days <= m).DefaultIfEmpty(0).Max();
            if (milestone > 0 && s.LastReminderDaysOut != milestone)
            {
                await notif.CreateAsync(
                    title:      $"📺 DStv renewal due in {milestone} day{(milestone == 1 ? "" : "s")}: {s.Location}",
                    message:    $"DStv decoder {s.DecoderNumber} ({s.Package}) at {s.Location} expires on {s.ExpiryDate:d MMM yyyy}.",
                    type:       NotificationType.SubscriptionExpiring,
                    module:     "DStv",
                    entityId:   s.Id.ToString(),
                    refNumber:  s.DecoderNumber,
                    targetRole: NotificationTarget.Management);
                s.LastReminderDaysOut = milestone;
                s.UpdatedAt = now;
                changed = true;
            }
        }

        if (changed) await db.SaveChangesAsync(ct);
    }

    // ── Vehicle statutory-document renewal reminders (14 / 7 / 1 days) ─────────
    private static readonly int[] VehicleDocMilestones = [14, 7, 1];

    private async Task RunVehicleDocumentRemindersAsync(
        GenServiceDbContext db, NotificationService notif, DateTime now, CancellationToken ct)
    {
        var docs    = await db.VehicleDocuments.ToListAsync(ct);
        var changed = false;

        foreach (var d in docs)
        {
            var days = (int)Math.Ceiling((d.ExpiryDate.Date - now.Date).TotalDays);

            if (days < 0)
            {
                d.Status = VehicleDocumentStatus.Expired;
                if (!d.ExpiredNotified)
                {
                    await notif.CreateAsync(
                        title:      $"🚗 {DocLabel(d.DocumentType)} EXPIRED: {d.VehicleRegNo}",
                        message:    $"{DocLabel(d.DocumentType)} for {d.VehicleRegNo} expired on {d.ExpiryDate:d MMM yyyy}. Renew immediately.",
                        type:       NotificationType.VehicleDocExpired,
                        module:     "Vehicle",
                        entityId:   d.Id.ToString(),
                        refNumber:  d.VehicleRegNo,
                        targetRole: NotificationTarget.Management);
                    d.ExpiredNotified = true;
                    d.UpdatedAt = now;
                    changed = true;
                }
                continue;
            }

            d.Status = days <= 14 ? VehicleDocumentStatus.Expiring : VehicleDocumentStatus.Valid;

            var milestone = VehicleDocMilestones.Where(m => days <= m).DefaultIfEmpty(0).Max();
            if (milestone > 0 && d.LastReminderDaysOut != milestone)
            {
                await notif.CreateAsync(
                    title:      $"🚗 {DocLabel(d.DocumentType)} renewal due in {milestone} day{(milestone == 1 ? "" : "s")}: {d.VehicleRegNo}",
                    message:    $"{DocLabel(d.DocumentType)} for {d.VehicleRegNo} expires on {d.ExpiryDate:d MMM yyyy}.",
                    type:       NotificationType.VehicleDocExpiring,
                    module:     "Vehicle",
                    entityId:   d.Id.ToString(),
                    refNumber:  d.VehicleRegNo,
                    targetRole: NotificationTarget.Management);
                d.LastReminderDaysOut = milestone;
                d.UpdatedAt = now;
                changed = true;
            }
        }

        if (changed) await db.SaveChangesAsync(ct);
    }

    private static string DocLabel(string type) => type switch
    {
        VehicleDocumentType.VehicleLicence  => "Vehicle Licence",
        VehicleDocumentType.RoadWorthiness  => "Road Worthiness",
        VehicleDocumentType.Insurance       => "Insurance",
        VehicleDocumentType.HackneyPermit   => "Hackney Carriage Permit",
        VehicleDocumentType.HeavyDutyPermit => "Heavy Duty Permit",
        _                                    => type,
    };
}
