namespace GenService.API.Domain;

/// <summary>
/// Records a single generator runtime session — start, stop, fuel consumed, outage reason.
/// </summary>
public class GeneratorLog
{
    public Guid    Id       { get; set; } = Guid.NewGuid();
    public string  Location { get; set; } = "";          // e.g. "HQ Generator Room"

    // ── Session timing ─────────────────────────────────────────────────────────
    public DateTime  StartTime    { get; set; }
    public DateTime? EndTime      { get; set; }
    public double?   RuntimeHours { get; set; }          // stored for query convenience

    // ── Fuel ───────────────────────────────────────────────────────────────────
    public double? FuelLevelBefore { get; set; }         // litres
    public double? FuelLevelAfter  { get; set; }         // litres
    public double? FuelConsumed    { get; set; }         // litres

    // ── Classification ─────────────────────────────────────────────────────────
    public string  RunReason  { get; set; } = GeneratorRunReason.PowerOutage;
    public string  Status     { get; set; } = GeneratorLogStatus.Running;

    // ── Outage info (when RunReason == PowerOutage) ────────────────────────────
    public string? OutageCause { get; set; }

    // ── Notes & audit ──────────────────────────────────────────────────────────
    public string? Notes          { get; set; }
    public string  LoggedByEmail  { get; set; } = "";
    public string  LoggedByName   { get; set; } = "";

    // ── Meta ───────────────────────────────────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class GeneratorRunReason
{
    public const string PowerOutage    = "PowerOutage";
    public const string ScheduledTest  = "ScheduledTest";
    public const string Maintenance    = "Maintenance";
    public const string LoadShedding   = "LoadShedding";
    public const string Other          = "Other";
}

public static class GeneratorLogStatus
{
    public const string Running   = "Running";
    public const string Stopped   = "Stopped";
}
