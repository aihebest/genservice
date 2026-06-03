namespace GenService.API.Domain;

/// <summary>
/// Daily diesel tank level reading per location/tank.
/// System auto-calculates consumption = PreviousLevel − CurrentLevel.
/// Replaces the manual daily tracking that was done in Excel.
/// </summary>
public class DieselTankReading
{
    public Guid     Id             { get; set; } = Guid.NewGuid();

    // ── Which tank ────────────────────────────────────────────────────────────
    public string   Location       { get; set; } = "";    // PHC Office, DR, Woji, etc.
    public string   TankIdentifier { get; set; } = "";    // "Main Generator Tank", "Overhead Tank"

    // ── Reading ───────────────────────────────────────────────────────────────
    public DateTime ReadingDate       { get; set; } = DateTime.UtcNow.Date;
    public double   TankLevelLitres   { get; set; }     // current diesel level in tank

    // ── Auto-calculated fields ────────────────────────────────────────────────
    public double?  PreviousLevelLitres    { get; set; }  // previous day's reading (fetched server-side)
    public double?  ConsumptionLitres      { get; set; }  // PreviousLevel - CurrentLevel (auto-calc)

    // ── Cost calculation (optional) ───────────────────────────────────────────
    public decimal? CostPerLitreNaira  { get; set; }
    public decimal? TotalConsumptionCostNaira { get; set; }  // ConsumptionLitres × CostPerLitre

    public string?  Notes          { get; set; }

    // ── Audit ─────────────────────────────────────────────────────────────────
    public string   LoggedByEmail  { get; set; } = "";
    public string   LoggedByName   { get; set; } = "";
    public DateTime CreatedAt      { get; set; } = DateTime.UtcNow;
}
