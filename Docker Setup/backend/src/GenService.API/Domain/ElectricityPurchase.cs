namespace GenService.API.Domain;

/// <summary>
/// A single electricity purchase for a location — either PHED (postpaid/billed)
/// or Prepaid (token-based). Both are captured under one interface (Obinna's spec).
///
/// The system keeps a running unit balance per location: each purchase tops up the
/// balance, and a prepaid meter reading (remaining units) overrides it when supplied.
/// A Low Balance notification fires when the balance falls to/below the threshold.
/// </summary>
public class ElectricityPurchase
{
    public Guid     Id           { get; set; } = Guid.NewGuid();

    // ── Classification ────────────────────────────────────────────────────────
    public string   PurchaseType { get; set; } = ElectricityType.PHED;   // PHED | Prepaid
    public string   Location     { get; set; } = "";                     // WOJI, DGS, OFFICE, DR, UYO – Chairman, UYO – MD

    // ── Purchase details ──────────────────────────────────────────────────────
    public DateTime PurchaseDate { get; set; } = DateTime.UtcNow.Date;
    public string?  Vendor       { get; set; }                           // vendor/supplier (PHED) or agent (prepaid)
    public decimal  AmountNaira  { get; set; }                           // amount spent
    public double   UnitsKwh     { get; set; }                           // units purchased/allocated (PHED) or loaded (prepaid)

    // ── PHED-specific ─────────────────────────────────────────────────────────
    public string?  PaymentReference { get; set; }                       // payment ref / PO number

    // ── Prepaid-specific ──────────────────────────────────────────────────────
    public string?  TokenNumber      { get; set; }                       // recharge token
    public double?  MeterReadingKwh  { get; set; }                       // current meter reading (remaining units), if available

    // ── Attachments ───────────────────────────────────────────────────────────
    public string?  ReceiptAttachment { get; set; }                      // invoice/receipt filename or URL

    // ── Running balance & status ──────────────────────────────────────────────
    public double   LowBalanceThresholdKwh { get; set; } = 50;           // configurable threshold
    public double   BalanceAfterKwh        { get; set; }                 // running balance after this record
    public string   Status                 { get; set; } = ElectricityStatus.Active;

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

public static class ElectricityType
{
    public const string PHED    = "PHED";
    public const string Prepaid = "Prepaid";

    public static readonly string[] All = [PHED, Prepaid];
}

public static class ElectricityStatus
{
    public const string Active     = "Active";      // healthy balance
    public const string LowBalance = "LowBalance";  // at/below threshold
    public const string Depleted   = "Depleted";    // zero or negative balance
}

/// <summary>The six locations tracked for electricity management (Obinna's spec).</summary>
public static class ElectricityLocations
{
    public static readonly string[] All =
    [
        "WOJI",
        "DGS",
        "OFFICE",
        "DR",
        "UYO – Chairman",
        "UYO – MD",
    ];
}
