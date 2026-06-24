namespace GenService.API.Models;

// ── Inbound ────────────────────────────────────────────────────────────────

public record CreateRequestRequest(
    string  Title,
    string  Description,
    string  Category,
    string  Priority,
    string  Location,
    // Required for approval-needed categories so the email approval link can be sent
    string? LineManagerEmail = null,
    string? LineManagerName  = null
);

public record UpdateStatusRequest(string Status, string? Notes);

public record ApproveRequestRequest(string? Notes);

public record RejectRequestRequest(string Reason);

public record AssignRequestRequest(string AssigneeEmail, string AssigneeName);

public record ReassignRequestRequest(
    string  ReassignToType,  // Logistics | Vendor | Procurement | Internal
    string  ReassignToName,  // team / vendor / person name
    string? Notes
);

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
    // Line Manager (Stage 1)
    string?   LineManagerEmail,
    string?   LineManagerName,
    DateTime? LineManagerApprovedAt,
    // GS Approval (Stage 2)
    string? ApprovedByEmail,
    string? ApprovedByName,
    DateTime  CreatedAt,
    DateTime  UpdatedAt,
    DateTime? ApprovedAt,
    DateTime? CompletedAt,
    string?   RejectionReason,
    string?   Notes,
    // Reassignment
    string?   ReassignedToType,
    string?   ReassignedToName,
    string?   ReassignedNotes,
    DateTime? ReassignedAt
);

public record RequestListResponse(
    IEnumerable<RequestDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
