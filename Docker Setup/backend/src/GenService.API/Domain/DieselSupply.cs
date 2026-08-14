namespace GenService.API.Domain;

/// <summary>
/// A bulk diesel purchase into storage. Each supply maintains its own running
/// remaining balance as diesel is distributed from it, giving full traceability
/// from litre purchased to litre distributed (Obinna's spec).
/// </summary>
public class DieselBulkSupply
{
    public Guid     Id { get; set; } = Guid.NewGuid();

    /// <summary>Auto-generated reference, e.g. DSL/26/001.</summary>
    public string   SupplyReference { get; set; } = "";

    // ── Purchase ──────────────────────────────────────────────────────────────
    public DateTime SupplyDate      { get; set; } = DateTime.UtcNow.Date;
    public string   Vendor          { get; set; } = "";
    public string?  InvoiceNumber   { get; set; }             // invoice / waybill number
    public double   QuantityLitres  { get; set; }             // quantity supplied
    public decimal  UnitPriceNaira  { get; set; }             // per litre
    public decimal  TotalCostNaira  { get; set; }             // auto = qty × unit price
    public string?  StorageLocation { get; set; }
    public string?  DeliveryDocuments { get; set; }           // attachment reference
    public string?  ReceivingOfficer  { get; set; }

    // ── Running balance ───────────────────────────────────────────────────────
    public double   QuantityRemainingLitres { get; set; }     // decreases as diesel is distributed

    public string?  Notes         { get; set; }

    // ── Audit ─────────────────────────────────────────────────────────────────
    public string   LoggedByEmail { get; set; } = "";
    public string   LoggedByName  { get; set; } = "";
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt     { get; set; } = DateTime.UtcNow;

    // ── Edit audit (manager corrections) ──────────────────────────────────────
    public string?   LastEditedByName { get; set; }
    public DateTime? LastEditedAt     { get; set; }
}

/// <summary>
/// A diesel distribution from a bulk supply, either to a vehicle or to an
/// operational location. Reduces the source supply's remaining balance.
/// </summary>
public class DieselDistribution
{
    public Guid     Id { get; set; } = Guid.NewGuid();

    /// <summary>Auto-generated reference, e.g. DDT/26/001.</summary>
    public string   DistributionReference { get; set; } = "";

    public string   DistributionType { get; set; } = DieselDistributionType.Vehicle;  // Vehicle | Location
    public string   SupplyType       { get; set; } = "Regular";   // Regular | Extra (extra/top-up supply)

    // ── Source ────────────────────────────────────────────────────────────────
    public Guid     BulkSupplyId        { get; set; }
    public string   BulkSupplyReference { get; set; } = "";

    // ── Distribution ──────────────────────────────────────────────────────────
    public DateTime DistributionDate { get; set; } = DateTime.UtcNow.Date;
    public double   QuantityLitres   { get; set; }
    public string?  Purpose          { get; set; }

    // ── Vehicle recipient (when DistributionType == Vehicle) ──────────────────
    public string?  VehicleRegNo     { get; set; }
    public string?  Driver           { get; set; }
    public string?  OdometerReading  { get; set; }

    // ── Location recipient (when DistributionType == Location) ────────────────
    public string?  DestinationLocation { get; set; }

    // ── Officers & acknowledgement ────────────────────────────────────────────
    public string?  IssuingOfficer          { get; set; }
    public string?  ReceivingOfficer        { get; set; }
    public bool     RecipientAcknowledged   { get; set; } = false;

    public string?  Notes         { get; set; }

    // ── Audit ─────────────────────────────────────────────────────────────────
    public string   LoggedByEmail { get; set; } = "";
    public string   LoggedByName  { get; set; } = "";
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt     { get; set; } = DateTime.UtcNow;

    // ── Edit audit (manager corrections) ──────────────────────────────────────
    public string?   LastEditedByName { get; set; }
    public DateTime? LastEditedAt     { get; set; }
}

public static class DieselDistributionType
{
    public const string Vehicle  = "Vehicle";
    public const string Location = "Location";

    public static readonly string[] All = [Vehicle, Location];
}
