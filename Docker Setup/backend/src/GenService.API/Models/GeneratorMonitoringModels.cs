namespace GenService.API.Models;

// ── Generator Daily Reading ───────────────────────────────────────────────────

public record CreateGeneratorReadingRequest(
    string  AssetNo,
    string  AssetDescription,
    string  Location,
    double  CumulativeRunHours,
    double  RunHoursToday,
    string  GeneratorStatus,
    double  FuelLevelLitres,
    double? FuelConsumedLitres,
    double? UtilityAvailableHours,
    double  ServiceIntervalHours,
    double? LastServicedAtHours,
    string? Notes
);

public record GeneratorReadingDto(
    Guid      Id,
    string    AssetNo,
    string    AssetDescription,
    string    Location,
    DateTime  ReadingDate,
    double    CumulativeRunHours,
    double    RunHoursToday,
    string    GeneratorStatus,
    double    FuelLevelLitres,
    double?   FuelConsumedLitres,
    double?   UtilityAvailableHours,
    double    ServiceIntervalHours,
    double?   LastServicedAtHours,
    bool      ServiceAlertActive,
    double    HoursUntilNextService,
    string?   Notes,
    string    LoggedByEmail,
    string    LoggedByName,
    DateTime  CreatedAt
);

public record GeneratorReadingQuery(
    string? Location = null,
    string? AssetNo  = null,
    int     Days     = 30,
    int     Page     = 1,
    int     PageSize = 20
);

public record GeneratorSummaryByLocation(
    string Location,
    string AssetNo,
    string AssetDescription,
    double LatestCumulativeHours,
    double HoursUntilNextService,
    bool   ServiceAlertActive,
    double LatestFuelLevel,
    string LatestStatus,
    DateTime LatestReadingDate
);

// ── Power Meter Reading ───────────────────────────────────────────────────────

public record CreatePowerMeterReadingRequest(
    string   Location,
    string   MeterNumber,
    double   MeterReadingKwh,
    double?  UtilityAvailableHours,
    decimal? CostPerKwhNaira,
    string?  Notes
);

public record PowerMeterReadingDto(
    Guid      Id,
    string    Location,
    string    MeterNumber,
    DateTime  ReadingDate,
    double    MeterReadingKwh,
    double?   UnitsConsumedToday,
    double?   UtilityAvailableHours,
    decimal?  CostPerKwhNaira,
    decimal?  TotalElectricityCostNaira,
    string?   Notes,
    string    LoggedByEmail,
    string    LoggedByName,
    DateTime  CreatedAt
);

public record PowerMeterQuery(
    string? Location   = null,
    int     Days       = 30,
    int     Page       = 1,
    int     PageSize   = 20
);
