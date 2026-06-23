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
    }
}
