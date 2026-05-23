namespace GenService.API.Domain;

/// <summary>
/// Tracks diesel purchases, dispensing to generators/vehicles, and stock transfers.
/// </summary>
public class DieselRecord
{
    public Guid     Id         { get; set; } = Guid.NewGuid();
    public DateTime RecordDate { get; set; } = DateTime.UtcNow;

    // ── Transaction type ───────────────────────────────────────────────────────
    public string RecordType { get; set; } = DieselRecordType.Purchase;  // Purchase | Dispensed | Transfer

    // ── Quantity & cost ────────────────────────────────────────────────────────
    public double  QuantityLitres { get; set; }
    public decimal UnitCostNaira  { get; set; }   // per litre
    public decimal TotalCostNaira { get; set; }

    // ── Source / destination ───────────────────────────────────────────────────
    public string? Supplier        { get; set; }  // for Purchase records
    public string? Destination     { get; set; }  // generator ID, vehicle plate, or location

    // ── Requester ──────────────────────────────────────────────────────────────
    public string  RequestedByEmail { get; set; } = "";
    public string  RequestedByName  { get; set; } = "";

    // ── Approval (required for Dispensed) ─────────────────────────────────────
    public string?   ApprovedByEmail { get; set; }
    public string?   ApprovedByName  { get; set; }
    public DateTime? ApprovedAt      { get; set; }

    // ── Notes & audit ──────────────────────────────────────────────────────────
    public string? Notes     { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class DieselRecordType
{
    public const string Purchase  = "Purchase";   // bought from supplier
    public const string Dispensed = "Dispensed";  // given to generator or vehicle
    public const string Transfer  = "Transfer";   // moved between sites/stores
}
