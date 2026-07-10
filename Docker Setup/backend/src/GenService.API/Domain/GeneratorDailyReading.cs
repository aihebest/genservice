namespace GenService.API.Domain;

/// <summary>
/// Daily snapshot reading for each generator at each location.
/// Records engine-hour meter readings, fuel level, utility (NEPA) availability,
/// and a running "remaining hours to service" countdown that triggers service alerts.
/// One record per generator per day.
///
/// Henry's spec (July 2026):
///   • Run Hours (24h)   = CurrentEngineReading − PreviousEngineReading
///   • Remaining Service = deducts daily run hours from a 250h interval, reset on service
///   • Fuel Consumed     = PreviousFuelLevel − CurrentFuelLevel
///   • Utility Available  = CurrentUtilityReading − PreviousUtilityReading (hour meter)
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

    // ── Generator engine-hour meter (Henry §1) ────────────────────────────────
    public double  PreviousEngineReading { get; set; }     // previous engine-hour meter reading
    public double  CurrentEngineReading  { get; set; }     // current engine-hour meter reading
    public double  RunHoursToday         { get; set; }     // = Current − Previous (auto)
    public double  CumulativeRunHours    { get; set; }     // = CurrentEngineReading (kept for reports)
    public string  GeneratorStatus       { get; set; } = GeneratorDailyStatus.Standby;

    // ── Fuel level (Henry §3) ─────────────────────────────────────────────────
    public double  PreviousFuelLevelLitres { get; set; }   // previous fuel level (L)
    public double  FuelLevelLitres         { get; set; }   // current fuel level (L)
    public double? FuelConsumedLitres      { get; set; }   // = Previous − Current (auto)

    // ── Utility (NEPA) power (Henry §4) ───────────────────────────────────────
    public double? PreviousUtilityReading  { get; set; }   // previous utility hour-meter reading
    public double? CurrentUtilityReading   { get; set; }   // current utility hour-meter reading
    public double? UtilityAvailableHours   { get; set; }   // = Current − Previous (auto)

    // ── Service interval & remaining-hours countdown (Henry §2) ───────────────
    public double  ServiceIntervalHours { get; set; } = 250;   // service every N hours
    public double  RemainingServiceHours{ get; set; } = 250;   // running countdown, reset on service
    public bool    ServiceCompleted     { get; set; } = false; // a service was recorded with this reading
    public double? LastServicedAtHours  { get; set; }          // cumulative hours at last service
    public bool    ServiceAlertActive   { get; set; } = false; // true when RemainingServiceHours <= alert threshold

    /// <summary>Threshold (hours remaining) at or below which a service alert is raised.</summary>
    public const double ServiceAlertThresholdHours = 150;

    // ── Computed helpers (not persisted) ──────────────────────────────────────
    public double HoursSinceLastService =>
        LastServicedAtHours.HasValue
            ? CumulativeRunHours - LastServicedAtHours.Value
            : CumulativeRunHours;

    /// <summary>Alias kept for existing UI/reports — mirrors RemainingServiceHours.</summary>
    public double HoursUntilNextService => RemainingServiceHours;

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
