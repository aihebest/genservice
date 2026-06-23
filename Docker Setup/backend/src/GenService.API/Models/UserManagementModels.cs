namespace GenService.API.Models;

// ── Query ──────────────────────────────────────────────────────────────────────
public record UserListQuery(
    string? Role       = null,
    string? Department = null,
    bool?   IsActive   = null,
    string? Search     = null,   // matches email or fullName
    int     Page       = 1,
    int     PageSize   = 30
);

// ── Request payloads ──────────────────────────────────────────────────────────
public record CreateUserRequest(
    string Email,
    string FullName,
    string Role,
    string Department,
    string Password          // plain-text — hashed before storage
);

public record UpdateUserRequest(
    string? FullName   = null,
    string? Role       = null,
    string? Department = null
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

// ── DTO ───────────────────────────────────────────────────────────────────────
public record AppUserDto(
    Guid      Id,
    string    Email,
    string    FullName,
    string    Role,
    string    Department,
    bool      IsActive,
    DateTime? LastLoginAt,
    string?   CreatedByEmail,
    DateTime  CreatedAt,
    DateTime  UpdatedAt
);

// ── List response ─────────────────────────────────────────────────────────────
public record UserListResponse(
    IEnumerable<AppUserDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

// ── Reset-password response ───────────────────────────────────────────────────
public record ResetPasswordResponse(
    string Email,
    string TemporaryPassword,
    string Message
);
