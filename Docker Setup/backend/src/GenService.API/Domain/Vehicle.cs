namespace GenService.API.Domain;

/// <summary>
/// Master registry record for a company vehicle. Statutory documents
/// (<see cref="VehicleDocument"/>) are held as independent, expiry-tracked records.
/// </summary>
public class Vehicle
{
    public Guid     Id { get; set; } = Guid.NewGuid();

    // ── Identity ──────────────────────────────────────────────────────────────
    public string   FleetNumber        { get; set; } = "";
    public string   RegistrationNumber { get; set; } = "";   // unique plate, e.g. PHC 185 AM
    public string   VehicleType        { get; set; } = "";   // Pickup, SUV, Bus, Car, Truck…
    public string?  MakeModel          { get; set; }         // e.g. Toyota Hilux
    public int?     YearOfManufacture  { get; set; }
    public string?  EngineNumber       { get; set; }
    public string?  ChassisNumber      { get; set; }
    public string?  Colour             { get; set; }

    // ── Assignment ────────────────────────────────────────────────────────────
    public string?  AssignedLocation   { get; set; }
    public string?  AssignedDriver     { get; set; }
    public DateTime? AcquisitionDate   { get; set; }
    public string   OperationalStatus  { get; set; } = VehicleOperationalStatus.Active;

    public string?  Notes         { get; set; }

    // ── Audit ─────────────────────────────────────────────────────────────────
    public string   LoggedByEmail { get; set; } = "";
    public string   LoggedByName  { get; set; } = "";
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt     { get; set; } = DateTime.UtcNow;
}

public static class VehicleOperationalStatus
{
    public const string Active     = "Active";
    public const string InWorkshop = "InWorkshop";
    public const string Grounded   = "Grounded";
    public const string Disposed   = "Disposed";

    public static readonly string[] All = [Active, InWorkshop, Grounded, Disposed];
}

/// <summary>
/// A statutory document for a vehicle (Licence, Road Worthiness, Insurance,
/// Hackney Carriage Permit, Heavy Duty Permit). Expiry is monitored to drive
/// 14/7/1-day renewal reminders and auto-expiry.
/// </summary>
public class VehicleDocument
{
    public Guid     Id        { get; set; } = Guid.NewGuid();

    // ── Owning vehicle ────────────────────────────────────────────────────────
    public Guid     VehicleId    { get; set; }
    public string   VehicleRegNo { get; set; } = "";   // denormalised for convenient display

    // ── Document details ──────────────────────────────────────────────────────
    public string   DocumentType     { get; set; } = VehicleDocumentType.VehicleLicence;
    public DateTime IssueDate        { get; set; } = DateTime.UtcNow.Date;
    public DateTime ExpiryDate       { get; set; }
    public string?  IssuingAuthority { get; set; }
    public decimal? RenewalCostNaira { get; set; }
    public string?  ReceiptAttachment { get; set; }

    // ── Status & reminder tracking ────────────────────────────────────────────
    public string   Status          { get; set; } = VehicleDocumentStatus.Valid;
    public int?     LastReminderDaysOut { get; set; }   // 14, 7, 1 milestone already sent
    public bool     ExpiredNotified { get; set; } = false;

    public string?  Notes         { get; set; }

    // ── Audit ─────────────────────────────────────────────────────────────────
    public string   LoggedByEmail { get; set; } = "";
    public string   LoggedByName  { get; set; } = "";
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt     { get; set; } = DateTime.UtcNow;
}

public static class VehicleDocumentType
{
    public const string VehicleLicence     = "VehicleLicence";
    public const string RoadWorthiness     = "RoadWorthiness";
    public const string Insurance          = "Insurance";
    public const string HackneyPermit      = "HackneyPermit";
    public const string HeavyDutyPermit    = "HeavyDutyPermit";

    public static readonly string[] All =
        [VehicleLicence, RoadWorthiness, Insurance, HackneyPermit, HeavyDutyPermit];
}

public static class VehicleDocumentStatus
{
    public const string Valid    = "Valid";     // more than 14 days to expiry
    public const string Expiring = "Expiring";  // within 14 days
    public const string Expired  = "Expired";   // past expiry
}
