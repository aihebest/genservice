namespace GenService.API.Models;

// ── Bulk diesel supply ─────────────────────────────────────────────────────────

public record CreateDieselSupplyRequest(
    string    Vendor,
    double    QuantityLitres,
    decimal   UnitPriceNaira,
    DateTime? SupplyDate       = null,
    string?   InvoiceNumber    = null,
    string?   StorageLocation  = null,
    string?   DeliveryDocuments= null,
    string?   ReceivingOfficer = null,
    string?   Notes            = null
);

public record DieselSupplyDto(
    Guid     Id,
    string   SupplyReference,
    DateTime SupplyDate,
    string   Vendor,
    string?  InvoiceNumber,
    double   QuantityLitres,
    decimal  UnitPriceNaira,
    decimal  TotalCostNaira,
    string?  StorageLocation,
    string?  DeliveryDocuments,
    string?  ReceivingOfficer,
    double   QuantityRemainingLitres,
    double   QuantityDistributedLitres,
    string?  Notes,
    string   LoggedByEmail,
    string   LoggedByName,
    DateTime CreatedAt,
    string?   LastEditedByName = null,
    DateTime? LastEditedAt     = null
);

/// <summary>Manager correction of a bulk supply batch. Only supplied fields change.</summary>
public record UpdateDieselSupplyRequest(
    string?   Vendor            = null,
    double?   QuantityLitres    = null,
    decimal?  UnitPriceNaira    = null,
    DateTime? SupplyDate        = null,
    string?   InvoiceNumber     = null,
    string?   StorageLocation   = null,
    string?   DeliveryDocuments = null,
    string?   ReceivingOfficer  = null,
    string?   Notes             = null
);

public record DieselSupplyQuery(
    int      Days     = 180,
    int      Page     = 1,
    int      PageSize = 20
);

// ── Diesel distribution ────────────────────────────────────────────────────────

public record CreateDieselDistributionRequest(
    string    DistributionType,      // Vehicle | Location
    string    BulkSupplyReference,   // free-text; matched to a batch to decrement/enforce balance
    double    QuantityLitres,
    string?   SupplyType         = null,   // Regular | Extra (extra/top-up supply)
    DateTime? DistributionDate    = null,
    string?   Purpose             = null,
    string?   VehicleRegNo        = null,
    string?   Driver              = null,
    string?   OdometerReading     = null,
    string?   DestinationLocation = null,
    string?   IssuingOfficer      = null,
    string?   ReceivingOfficer    = null,
    bool      RecipientAcknowledged = false,
    string?   Notes               = null
);

public record DieselDistributionDto(
    Guid     Id,
    string   DistributionReference,
    string   DistributionType,
    string   SupplyType,
    Guid     BulkSupplyId,
    string   BulkSupplyReference,
    DateTime DistributionDate,
    double   QuantityLitres,
    string?  Purpose,
    string?  VehicleRegNo,
    string?  Driver,
    string?  OdometerReading,
    string?  DestinationLocation,
    string?  IssuingOfficer,
    string?  ReceivingOfficer,
    bool     RecipientAcknowledged,
    string?  Notes,
    string   LoggedByEmail,
    string   LoggedByName,
    DateTime CreatedAt,
    // ── Vehicle consumption (Option B) — derived from the vehicle's previous issue ──
    DateTime? PreviousDistributionDate = null,
    double?   PreviousQuantityLitres   = null,
    double?   PreviousOdometer         = null,
    /// <summary>Odometer now − odometer at the previous issue.</summary>
    double?   DistanceKm               = null,
    /// <summary>Distance ÷ litres issued last time (the fuel that covered it).</summary>
    double?   KmPerLitre               = null
);

/// <summary>Per-vehicle diesel consumption over a period.</summary>
public record VehicleDieselSummaryDto(
    string    VehicleRegNo,
    int       Issues,
    double    TotalLitres,
    DateTime  LastIssueDate,
    double?   LastOdometer,
    /// <summary>Total km covered between issues that have odometer readings.</summary>
    double?   DistanceKm,
    /// <summary>Litres that covered that distance (each issue's litres, up to the next issue).</summary>
    double?   LitresForDistance,
    double?   AverageKmPerLitre
);

public record DieselDistributionQuery(
    string?  DistributionType = null,
    Guid?    BulkSupplyId     = null,
    string?  VehicleRegNo     = null,
    int      Days             = 180,
    int      Page             = 1,
    int      PageSize         = 20
);

/// <summary>Overall diesel stock position.</summary>
public record DieselStockSummaryDto(
    double  TotalSuppliedLitres,
    double  TotalDistributedLitres,
    double  AvailableBalanceLitres,
    decimal TotalPurchaseValueNaira,
    int     ActiveSupplyBatches
);
