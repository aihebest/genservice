namespace GenService.API.Models;

// ── Generator Daily Reading ───────────────────────────────────────────────────

public record CreateGeneratorReadingRequest(
    string  AssetNo,
    string  AssetDescription,
    string  Location,
    double  CurrentEngineReading,        // §1 current engine-hour meter reading (required)
    string  GeneratorStatus,
    double  CurrentFuelLevelLitres,      // §3 current fuel level (required)
    double? PreviousEngineReading  = null, // optional — auto-filled from last reading
    double? PreviousFuelLevelLitres= null, // optional — auto-filled from last reading
    double? PreviousUtilityReading = null, // §4 optional — auto-filled from last reading
    double? CurrentUtilityReading  = null, // §4 current utility hour-meter reading
    double  ServiceIntervalHours   = 250,
    bool    ServiceCompleted       = false, // §2 tick when a service was carried out
    double? LastServicedAtHours    = null,
    string?   Notes                = null,
    DateTime? ReadingDate          = null   // optional — allows backdating a reading
);

public record GeneratorReadingDto(
    Guid      Id,
    string    AssetNo,
    string    AssetDescription,
    string    Location,
    DateTime  ReadingDate,
    double    PreviousEngineReading,
    double    CurrentEngineReading,
    double    CumulativeRunHours,
    double    RunHoursToday,
    string    GeneratorStatus,
    double    PreviousFuelLevelLitres,
    double    FuelLevelLitres,
    double?   FuelConsumedLitres,
    double?   PreviousUtilityReading,
    double?   CurrentUtilityReading,
    double?   UtilityAvailableHours,
    double    ServiceIntervalHours,
    double    RemainingServiceHours,
    bool      ServiceCompleted,
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
    string    Location,
    string    MeterNumber,
    double    PreviousMeterReading,
    double    CurrentMeterReading,
    DateTime? ReadingDate           = null,
    double?   UtilityAvailableHours = null,
    string?   Notes                 = null
);

public record PowerMeterReadingDto(
    Guid      Id,
    string    Location,
    string    MeterNumber,
    DateTime  ReadingDate,
    double    PreviousMeterReading,
    double    CurrentMeterReading,
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
