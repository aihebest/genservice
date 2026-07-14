namespace GenService.API.Models;

// ── Vehicle master registry ────────────────────────────────────────────────────

public record CreateVehicleRequest(
    string    FleetNumber,
    string    RegistrationNumber,
    string    VehicleType,
    string?   MakeModel         = null,
    int?      YearOfManufacture = null,
    string?   EngineNumber      = null,
    string?   ChassisNumber     = null,
    string?   Colour            = null,
    string?   AssignedLocation  = null,
    string?   AssignedDriver    = null,
    DateTime? AcquisitionDate   = null,
    string?   OperationalStatus = null,
    string?   Notes             = null
);

public record VehicleDto(
    Guid      Id,
    string    FleetNumber,
    string    RegistrationNumber,
    string    VehicleType,
    string?   MakeModel,
    int?      YearOfManufacture,
    string?   EngineNumber,
    string?   ChassisNumber,
    string?   Colour,
    string?   AssignedLocation,
    string?   AssignedDriver,
    DateTime? AcquisitionDate,
    string    OperationalStatus,
    string?   Notes,
    int       DocumentCount,
    int       ExpiringDocumentCount,
    int       ExpiredDocumentCount,
    string    LoggedByEmail,
    string    LoggedByName,
    DateTime  CreatedAt
);

public record VehicleQuery(
    string?  Location = null,
    string?  Status   = null,
    string?  Search   = null,
    int      Page     = 1,
    int      PageSize = 20
);

// ── Vehicle statutory documents ────────────────────────────────────────────────

public record CreateVehicleDocumentRequest(
    string    VehicleRegNo,          // free-text vehicle registration (linked to registry if it matches)
    string    DocumentType,
    DateTime  ExpiryDate,
    DateTime? IssueDate         = null,
    string?   IssuingAuthority  = null,
    decimal?  RenewalCostNaira  = null,
    string?   ReceiptAttachment = null,
    string?   Notes             = null
);

public record RenewVehicleDocumentRequest(
    DateTime  ExpiryDate,
    DateTime? IssueDate         = null,
    decimal?  RenewalCostNaira  = null,
    string?   IssuingAuthority  = null,
    string?   ReceiptAttachment = null,
    string?   Notes             = null
);

public record VehicleDocumentDto(
    Guid      Id,
    Guid      VehicleId,
    string    VehicleRegNo,
    string    DocumentType,
    DateTime  IssueDate,
    DateTime  ExpiryDate,
    int       DaysToExpiry,
    string?   IssuingAuthority,
    decimal?  RenewalCostNaira,
    string?   ReceiptAttachment,
    string    Status,
    string?   Notes,
    string    LoggedByEmail,
    string    LoggedByName,
    DateTime  CreatedAt
);

public record VehicleDocumentQuery(
    Guid?    VehicleId    = null,
    string?  DocumentType = null,
    string?  Status       = null,
    int      Page         = 1,
    int      PageSize     = 50
);
