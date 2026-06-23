namespace GenService.API.Domain;

/// <summary>
/// Daily Routine Parameter Check — one record per location per day.
/// Captures power, water, fuel, staff, and facility status observations.
/// </summary>
public class DailyParameterLog
{
    public Guid     Id       { get; set; } = Guid.NewGuid();
    public DateOnly LogDate  { get; set; }
    public string   Location { get; set; } = "";

    // ── Power supply ────────────────────────────────────────────────────────────
    /// <summary>Hours NEPA (utility grid) was available today</summary>
    public double?  NepaHoursAvailable    { get; set; }
    /// <summary>Hours generator ran today (all units combined)</summary>
    public double?  GeneratorHoursRun     { get; set; }
    /// <summary>Litres of diesel consumed today</summary>
    public double?  DieselConsumedLitres  { get; set; }
    /// <summary>Diesel tank balance (litres) at end of day</summary>
    public double?  DieselBalanceLitres   { get; set; }
    /// <summary>Primary generator status at close of day: Running | Standby | Fault | Off</summary>
    public string?  GeneratorStatus       { get; set; }
    /// <summary>Running hour meter reading at end of day</summary>
    public double?  GeneratorRunHourMeter { get; set; }

    // ── Water supply ────────────────────────────────────────────────────────────
    /// <summary>Water source: Municipal | Borehole | Both | None</summary>
    public string?  WaterSource           { get; set; }
    /// <summary>Tank level at end of day (%)</summary>
    public double?  WaterTankLevelPercent { get; set; }
    /// <summary>Overall water status: Adequate | Low | Critical | Refilled</summary>
    public string?  WaterStatus           { get; set; }

    // ── Staff & occupancy ───────────────────────────────────────────────────────
    public int?     StaffPresent          { get; set; }
    public int?     ExpectedStaff         { get; set; }
    public int?     VisitorCount          { get; set; }

    // ── Facility checks ─────────────────────────────────────────────────────────
    /// <summary>Office cleaning done: true/false</summary>
    public bool     CleaningDone          { get; set; } = false;
    /// <summary>Waste disposed: true/false</summary>
    public bool     WasteDisposed         { get; set; } = false;
    /// <summary>Security status: Normal | Incident Reported</summary>
    public string?  SecurityStatus        { get; set; }

    // ── Observations & actions ──────────────────────────────────────────────────
    public string?  MaintenanceIssues     { get; set; }   // issues noticed today
    public string?  ActionsTaken          { get; set; }   // actions taken same day
    public string?  PendingActions        { get; set; }   // outstanding/follow-up
    public string?  GeneralRemarks        { get; set; }

    // ── Logger ──────────────────────────────────────────────────────────────────
    public string   LoggedByEmail         { get; set; } = "";
    public string   LoggedByName          { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class GeneratorStatusValue
{
    public const string Running  = "Running";
    public const string Standby  = "Standby";
    public const string Fault    = "Fault";
    public const string Off      = "Off";
}

public static class WaterSourceValue
{
    public const string Municipal = "Municipal";
    public const string Borehole  = "Borehole";
    public const string Both      = "Both";
    public const string None      = "None";
}

public static class WaterStatusValue
{
    public const string Adequate  = "Adequate";
    public const string Low       = "Low";
    public const string Critical  = "Critical";
    public const string Refilled  = "Refilled";
}
