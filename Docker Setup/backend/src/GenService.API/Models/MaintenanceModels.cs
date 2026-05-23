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
    string   CompletedByName
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
    DateTime  UpdatedAt
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
