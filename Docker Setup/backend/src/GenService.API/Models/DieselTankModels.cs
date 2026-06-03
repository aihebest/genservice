namespace GenService.API.Models;

public record CreateDieselTankReadingRequest(
    string   Location,
    string   TankIdentifier,
    double   TankLevelLitres,
    decimal? CostPerLitreNaira,
    string?  Notes
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
    DateTime  CreatedAt
);

public record DieselTankQuery(
    string? Location       = null,
    string? TankIdentifier = null,
    int     Days           = 30,
    int     Page           = 1,
    int     PageSize       = 20
);
