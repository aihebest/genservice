namespace GenService.API.Domain;

/// <summary>
/// Recurring maintenance schedule — tracks due dates, completion history,
/// and triggers overdue escalation logic.
/// </summary>
public class MaintenanceSchedule
{
    public Guid   Id             { get; set; } = Guid.NewGuid();
    public string TaskName       { get; set; } = "";
    public string Description    { get; set; } = "";
    public string Category       { get; set; } = MaintenanceCategory.General;
    public string Location       { get; set; } = "";

    // ── Recurrence ────────────────────────────────────────────────────────
    public string FrequencyLabel { get; set; } = "Monthly";   // "Weekly", "Monthly", "Quarterly", "Annually", "Custom"
    public int    FrequencyDays  { get; set; } = 30;          // # days between occurrences

    // ── Scheduling ────────────────────────────────────────────────────────
    public DateTime  NextDueAt        { get; set; }
    public DateTime? LastCompletedAt  { get; set; }
    public bool      IsOverdue        => DateTime.UtcNow > NextDueAt;

    // ── Assignment ────────────────────────────────────────────────────────
    public string? AssignedToEmail { get; set; }
    public string? AssignedToName  { get; set; }

    // ── Completion tracking ───────────────────────────────────────────────
    public string?   LastCompletedByEmail { get; set; }
    public string?   LastCompletedByName  { get; set; }
    public string?   LastCompletionNotes  { get; set; }

    // ── Meta ──────────────────────────────────────────────────────────────
    public bool     IsActive  { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class MaintenanceCategory
{
    // ── Equipment Maintenance ─────────────────────────────────────────────────
    public const string GeneratorService = "GeneratorService";
    public const string HVAC             = "HVAC";              // Air conditioners
    public const string UPS              = "UPS";
    public const string Pumps            = "Pumps";

    // ── Vehicle Maintenance ───────────────────────────────────────────────────
    public const string VehicleServicing  = "VehicleServicing";
    public const string VehicleInspection = "VehicleInspection";

    // ── Facility Maintenance ──────────────────────────────────────────────────
    public const string Electrical   = "Electrical";
    public const string Plumbing     = "Plumbing";
    public const string CivilWorks   = "CivilWorks";
    public const string FireSafety   = "FireSafety";
    public const string Fumigation   = "Fumigation";
    public const string WasteDisposal= "WasteDisposal";
    public const string TankWashing  = "TankWashing";
    public const string General      = "General";
}

// ── Maintenance group (top-level classification) ───────────────────────────────
public static class MaintenanceGroup
{
    public const string Equipment = "Equipment";
    public const string Vehicle   = "Vehicle";
    public const string Facility  = "Facility";

    public static string ForCategory(string category) => category switch
    {
        MaintenanceCategory.GeneratorService or
        MaintenanceCategory.HVAC             or
        MaintenanceCategory.UPS              or
        MaintenanceCategory.Pumps            => Equipment,

        MaintenanceCategory.VehicleServicing  or
        MaintenanceCategory.VehicleInspection => Vehicle,

        _ => Facility,
    };
}
