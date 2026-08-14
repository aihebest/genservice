namespace GenService.API.Models;

// ── Electricity Management ─────────────────────────────────────────────────────

public record CreateElectricityPurchaseRequest(
    string   PurchaseType,               // PHED | Prepaid
    string   Location,
    decimal  AmountNaira,
    double   UnitsKwh,
    DateTime? PurchaseDate          = null,
    string?  Vendor                 = null,
    string?  PaymentReference       = null,
    string?  TokenNumber            = null,
    double?  MeterReadingKwh        = null,
    string?  ReceiptAttachment      = null,
    double   LowBalanceThresholdKwh = 50,
    string?  Notes                  = null
);

public record ElectricityPurchaseDto(
    Guid     Id,
    string   PurchaseType,
    string   Location,
    DateTime PurchaseDate,
    string?  Vendor,
    decimal  AmountNaira,
    double   UnitsKwh,
    string?  PaymentReference,
    string?  TokenNumber,
    double?  MeterReadingKwh,
    string?  ReceiptAttachment,
    double   LowBalanceThresholdKwh,
    double   BalanceAfterKwh,
    string   Status,
    string?  Notes,
    string   LoggedByEmail,
    string   LoggedByName,
    DateTime CreatedAt,
    string?   LastEditedByName = null,
    DateTime? LastEditedAt     = null
);

public record ElectricityQuery(
    string?  PurchaseType = null,
    string?  Location     = null,
    int      Days         = 90,
    int      Page         = 1,
    int      PageSize     = 20
);

/// <summary>Latest running balance + status for a single location.</summary>
public record ElectricityBalanceDto(
    string   Location,
    double   BalanceKwh,
    double   LowBalanceThresholdKwh,
    string   Status,
    decimal  TotalSpendNaira,
    double   TotalUnitsPurchased,
    DateTime? LastPurchaseDate
);
