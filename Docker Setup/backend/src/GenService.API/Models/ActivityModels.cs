namespace GenService.API.Models;

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record LogActivityRequest(
    string StaffEmail,
    string StaffName,
    string ActivityDescription,
    string Category,
    string? Location,
    string? Notes,
    /// <summary>YYYY-MM-DD — the day the activity was done. Defaults to today.</summary>
    string? ActivityDate = null
);

public record ProxyLogRequest(
    string StaffEmail,
    string StaffName,
    string ActivityDescription,
    string Category,
    string? Location,
    string? Notes,
    /// <summary>YYYY-MM-DD — the day the activity was done. Defaults to today.</summary>
    string? ActivityDate = null
);

/// <summary>Manager correction of an activity entry. Only supplied fields change.</summary>
public record UpdateActivityRequest(
    string? StaffName           = null,
    string? ActivityDescription = null,
    string? Category            = null,
    string? Location            = null,
    string? Notes               = null,
    string? ActivityDate        = null,   // YYYY-MM-DD
    string? Status              = null
);

public record UpdateActivityStatusRequest(
    string Status,
    string? Notes
);

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record ActivityDto(
    Guid     Id,
    string   StaffEmail,
    string   StaffName,
    string   ActivityDescription,
    string   Location,
    string   Category,
    string   Status,
    bool     IsProxy,
    string   LoggedByEmail,
    string   LoggedByName,
    string?  Notes,
    DateTime StartedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt,
    /// <summary>The day the activity was done (falls back to the entry date for older records).</summary>
    DateTime? ActivityDate = null
);

public record ActivityListResponse(
    IReadOnlyList<ActivityDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
