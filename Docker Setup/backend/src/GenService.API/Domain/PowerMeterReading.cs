namespace GenService.API.Domain;

/// <summary>
/// Daily NPA/utility power meter reading per location.
/// Tracks kWh consumed and utility availability hours per day.
/// </summary>
public class PowerMeterReading
{
    public Guid     Id       { get; set; } = Guid.NewGuid();

    // ── Location ──────────────────────────────────────────────────────────────
    public string Location    { get; set; } = "";    // Lagos Office, PHC Office, DR, etc.
    public string MeterNumber { get; set; } = "";    // NPA meter serial / reference

    // ── Reading ───────────────────────────────────────────────────────────────
    public DateTime ReadingDate          { get; set; } = DateTime.UtcNow.Date;
    public double   PreviousMeterReading { get; set; }     // previous meter reading (user input)
    public double   CurrentMeterReading  { get; set; }     // current meter reading (user input)
    public double   MeterReadingKwh      { get; set; }     // = CurrentMeterReading (kept for reports)
    public double?  UnitsConsumedToday   { get; set; }     // = Current − Previous (auto, 24h)
    public double?  UtilityAvailableHours{ get; set; }     // hours NPA power was on

    // ── Cost calculation ──────────────────────────────────────────────────────
    public decimal? CostPerKwhNaira           { get; set; }  // configurable tariff rate
    public decimal? TotalElectricityCostNaira { get; set; }  // UnitsConsumedToday × CostPerKwhNaira

    public string?  Notes         { get; set; }

    // ── Audit ─────────────────────────────────────────────────────────────────
    public string   LoggedByEmail { get; set; } = "";
    public string   LoggedByName  { get; set; } = "";
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
}
