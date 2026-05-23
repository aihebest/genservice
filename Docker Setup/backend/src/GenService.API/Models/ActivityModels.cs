namespace GenService.API.Models;

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record LogActivityRequest(
    string StaffEmail,
    string StaffName,
    string ActivityDescription,
    string Category,
    string? Location,
    string? Notes
);

public record ProxyLogRequest(
    string StaffEmail,
    string StaffName,
    string ActivityDescription,
    string Category,
    string? Location,
    string? Notes
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
    DateTime? CompletedAt
);

public record ActivityListResponse(
    IReadOnlyList<ActivityDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
