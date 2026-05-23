namespace GenService.API.Models;

// ── Inbound ────────────────────────────────────────────────────────────────

public record CreateRequestRequest(
    string Title,
    string Description,
    string Category,
    string Priority,
    string Location
);

public record UpdateStatusRequest(string Status, string? Notes);

public record ApproveRequestRequest(string? Notes);

public record RejectRequestRequest(string Reason);

public record AssignRequestRequest(string AssigneeEmail, string AssigneeName);

// ── Query parameters ───────────────────────────────────────────────────────

public record RequestsQuery(
    string? Status     = null,
    string? Category   = null,
    string? Priority   = null,
    string? Search     = null,
    int     Page       = 1,
    int     PageSize   = 20
);

// ── Outbound ───────────────────────────────────────────────────────────────

public record RequestDto(
    Guid    Id,
    string  TicketNumber,
    string  Title,
    string  Description,
    string  Category,
    bool    RequiresApproval,
    string  Status,
    string  Priority,
    string  Location,
    string  RequestedByEmail,
    string  RequestedByName,
    string? AssignedToEmail,
    string? AssignedToName,
    string? ApprovedByEmail,
    string? ApprovedByName,
    DateTime  CreatedAt,
    DateTime  UpdatedAt,
    DateTime? ApprovedAt,
    DateTime? CompletedAt,
    string?   RejectionReason,
    string?   Notes
);

public record RequestListResponse(
    IEnumerable<RequestDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
