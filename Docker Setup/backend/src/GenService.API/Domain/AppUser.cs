namespace GenService.API.Domain;

/// <summary>
/// Platform user — stores credentials and profile for all GenService staff.
/// Passwords are hashed with BCrypt; plain-text is never persisted.
/// </summary>
public class AppUser
{
    public Guid     Id           { get; set; } = Guid.NewGuid();
    public string   Email        { get; set; } = "";
    public string   FullName     { get; set; } = "";

    /// <summary>
    /// BCrypt hash of the user's password.
    /// Use BCrypt.Net.BCrypt.HashPassword / VerifyPassword to handle this field.
    /// </summary>
    public string   PasswordHash { get; set; } = "";

    /// <summary>
    /// One of: SystemAdmin | DepartmentManager | Supervisor | Technician |
    ///         Driver | Requester | StoreOfficer
    /// </summary>
    public string   Role         { get; set; } = "Requester";

    public string   Department   { get; set; } = "General Service";

    /// <summary>Whether the account is active and can log in.</summary>
    public bool     IsActive     { get; set; } = true;

    public DateTime? LastLoginAt  { get; set; }

    /// <summary>Email of the SystemAdmin who created this account.</summary>
    public string?  CreatedByEmail { get; set; }

    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt    { get; set; } = DateTime.UtcNow;
}

/// <summary>Allowed role values.</summary>
public static class AppUserRoles
{
    public const string SystemAdmin        = "SystemAdmin";
    public const string DepartmentManager  = "DepartmentManager";
    public const string Supervisor         = "Supervisor";
    public const string Technician         = "Technician";
    public const string Driver             = "Driver";
    public const string Requester          = "Requester";
    public const string StoreOfficer       = "StoreOfficer";

    public static readonly string[] All =
    [
        SystemAdmin, DepartmentManager, Supervisor,
        Technician, Driver, Requester, StoreOfficer,
    ];
}
