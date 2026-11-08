namespace GenService.API.Models;

public record CreateDieselTankReadingRequest(
    string   Location,
    string   TankIdentifier,
    double   TankLevelLitres,
    decimal? CostPerLitreNaira,
    string?  Notes,
    /// <summary>Dip-stick reading in mm. For a calibrated tank the server converts
    /// it to litres from the tank's chart and ignores TankLevelLitres.</summary>
    double?  DipMm = null
);

public record DieselTankReadingDto(
    Guid      Id,
    string    Location,
    string    TankIdentifier,
    DateTime  ReadingDate,
    double    TankLevelLitres,
    double?   PreviousLevelLitres,
    double?   ConsumptionLitres,
    decimal?  CostPerLitreNaira,
    decimal?  TotalConsumptionCostNaira,
    string?   Notes,
    string    LoggedByEmail,
    string    LoggedByName,
    DateTime  CreatedAt,
    /// <summary>Dip reading the litres were calculated from, when calibrated.</summary>
    double?   DipMm = null
);

public record DieselTankQuery(
    string? Location       = null,
    string? TankIdentifier = null,
    int     Days           = 30,
    int     Page           = 1,
    int     PageSize       = 20
);
