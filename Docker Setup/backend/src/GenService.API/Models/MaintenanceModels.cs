namespace GenService.API.Models;

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record CreateScheduleRequest(
    string   TaskName,
    string?  Description,
    string   Category,
    string?  Location,
    string   FrequencyLabel,
    int      FrequencyDays,
    DateTime NextDueAt,
    string?  AssignedToEmail,
    string?  AssignedToName
);

public record CompleteScheduleRequest(
    string?  Notes,
    string   CompletedByEmail,
    string   CompletedByName,
    /// <summary>YYYY-MM-DD — when the work was actually done. Defaults to today.</summary>
    string?  CompletedOn = null,
    /// <summary>Who carried it out — staff member or vendor.</summary>
    string?  DoneBy      = null
);

/// <summary>One entry in the scheduler's completion history.</summary>
public record ScheduleCompletionDto(
    Guid     Id,
    Guid     ScheduleId,
    string   TaskName,
    string   Category,
    string?  Location,
    string   CompletedOn,     // YYYY-MM-DD
    string?  DoneBy,
    string?  Notes,
    string   LoggedByName,
    DateTime CreatedAt
);

public record UpdateScheduleRequest(
    string?   TaskName,
    string?   Description,
    string?   Category,
    string?   Location,
    string?   FrequencyLabel,
    int?      FrequencyDays,
    DateTime? NextDueAt,
    string?   AssignedToEmail,
    string?   AssignedToName,
    bool?     IsActive
);

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record ScheduleDto(
    Guid      Id,
    string    TaskName,
    string    Description,
    string    Category,
    string    Location,
    string    FrequencyLabel,
    int       FrequencyDays,
    DateTime  NextDueAt,
    DateTime? LastCompletedAt,
    bool      IsOverdue,
    string?   AssignedToEmail,
    string?   AssignedToName,
    string?   LastCompletedByEmail,
    string?   LastCompletedByName,
    string?   LastCompletionNotes,
    bool      IsActive,
    DateTime  CreatedAt,
    DateTime  UpdatedAt,
    // Reminder / escalation tracking
    int       EscalationLevel,
    DateTime? LastReminderSentAt,
    DateTime? LastEscalationSentAt
);

public record SnoozeReminderRequest(
    int  HoursToSnooze   // set LastReminderSentAt = now + X hours (delays next reminder)
);

public record ScheduleListResponse(
    IReadOnlyList<ScheduleDto> Items,
    int TotalCount,
    int OverdueCount,
    int Page,
    int PageSize
);

public record MaintenanceStatsDto(
    int Total,
    int Overdue,
    int DueSoon,   // due within next 7 days
    int Completed, // completed this month
    int Active
);
