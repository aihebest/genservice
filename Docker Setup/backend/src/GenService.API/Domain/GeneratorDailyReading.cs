namespace GenService.API.Domain;

/// <summary>
/// Daily snapshot reading for each generator at each location.
/// Records cumulative run hours, fuel level, and triggers service alerts.
/// One record per generator per day.
/// </summary>
public class GeneratorDailyReading
{
    public Guid     Id         { get; set; } = Guid.NewGuid();

    // ── Which generator ───────────────────────────────────────────────────────
    public string   AssetNo          { get; set; } = "";    // e.g. 6660003188
    public string   AssetDescription { get; set; } = "";    // e.g. DR Cummins 275KVA
    public string   Location         { get; set; } = "";    // DR, PHC Office, Woji, etc.

    // ── Reading date ──────────────────────────────────────────────────────────
    public DateTime ReadingDate { get; set; } = DateTime.UtcNow.Date;

    // ── Generator run data ────────────────────────────────────────────────────
    public double  CumulativeRunHours  { get; set; }     // meter reading at time of log
    public double  RunHoursToday       { get; set; }     // hours run since last reading
    public string  GeneratorStatus     { get; set; } = GeneratorDailyStatus.Standby;

    // ── Fuel level ────────────────────────────────────────────────────────────
    public double  FuelLevelLitres    { get; set; }      // current diesel level
    public double? FuelConsumedLitres { get; set; }      // consumed since last reading

    // ── Utility power ─────────────────────────────────────────────────────────
    public double? UtilityAvailableHours { get; set; }   // hours utility (NPA) was available

    // ── Service alert ─────────────────────────────────────────────────────────
    public double  ServiceIntervalHours { get; set; } = 250;   // service every N hours
    public double? LastServicedAtHours  { get; set; }          // cumulative hours at last service
    public bool    ServiceAlertActive   { get; set; } = false;  // true when approaching threshold

    // ── Computed helper (not persisted) ──────────────────────────────────────
    public double HoursSinceLastService =>
        LastServicedAtHours.HasValue
            ? CumulativeRunHours - LastServicedAtHours.Value
            : CumulativeRunHours;

    public double HoursUntilNextService =>
        ServiceIntervalHours - (HoursSinceLastService % ServiceIntervalHours);

    public string? Notes { get; set; }

    // ── Audit ─────────────────────────────────────────────────────────────────
    public string   LoggedByEmail { get; set; } = "";
    public string   LoggedByName  { get; set; } = "";
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt     { get; set; } = DateTime.UtcNow;
}

public static class GeneratorDailyStatus
{
    public const string Running          = "Running";
    public const string Standby          = "Standby";
    public const string UnderMaintenance = "UnderMaintenance";
    public const string Fault            = "Fault";
}
