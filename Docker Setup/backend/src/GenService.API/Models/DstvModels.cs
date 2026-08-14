namespace GenService.API.Models;

// ── DStv Subscription Management ───────────────────────────────────────────────

public record CreateDstvSubscriptionRequest(
    string    DecoderNumber,
    string    Location,
    string    Package,
    decimal   AmountNaira,
    DateTime? StartDate         = null,
    DateTime? EndDate           = null,   // explicit expiry/end date
    int       DurationMonths    = 0,      // optional fallback if EndDate not given
    string?   PaymentMethod     = null,
    string?   Vendor            = null,
    string?   ReceiptAttachment = null,
    string?   Notes             = null
);

/// <summary>Records a renewal — extends the subscription from its current expiry.</summary>
public record RenewDstvSubscriptionRequest(
    int       DurationMonths,
    decimal   AmountNaira,
    DateTime? RenewalDate       = null,
    string?   PaymentMethod     = null,
    string?   Vendor            = null,
    string?   ReceiptAttachment = null,
    string?   Notes             = null
);

public record DstvSubscriptionDto(
    Guid     Id,
    string   DecoderNumber,
    string   Location,
    string   Package,
    DateTime StartDate,
    int      DurationMonths,
    DateTime ExpiryDate,
    int      DaysToExpiry,
    decimal  AmountNaira,
    string?  PaymentMethod,
    string?  Vendor,
    string?  ReceiptAttachment,
    string   Status,
    string?  Notes,
    string   LoggedByEmail,
    string   LoggedByName,
    DateTime CreatedAt,
    string?   LastEditedByName = null,
    DateTime? LastEditedAt     = null
);

public record DstvQuery(
    string?  Status   = null,
    string?  Location = null,
    int      Page     = 1,
    int      PageSize = 20
);
