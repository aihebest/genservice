namespace GenService.API.Models;

// ════════════════════════════════════════════════════════════════════════════
//  Generator Log DTOs
// ════════════════════════════════════════════════════════════════════════════

public record StartGeneratorRequest(
    string   Location,
    string   RunReason,
    string?  OutageCause,
    double?  FuelLevelBefore,
    string?  Notes
);

public record StopGeneratorRequest(
    double?  FuelLevelAfter,
    string?  Notes
);

public record GeneratorLogDto(
    Guid      Id,
    string    Location,
    DateTime  StartTime,
    DateTime? EndTime,
    double?   RuntimeHours,
    double?   FuelLevelBefore,
    double?   FuelLevelAfter,
    double?   FuelConsumed,
    string    RunReason,
    string    Status,
    string?   OutageCause,
    string?   Notes,
    string    LoggedByEmail,
    string    LoggedByName,
    DateTime  CreatedAt
);

public record GeneratorLogListResponse(
    IReadOnlyList<GeneratorLogDto> Items,
    int    TotalCount,
    int    Page,
    int    PageSize
);

public record GeneratorStatsDto(
    double  TotalRuntimeHoursThisMonth,
    double  TotalFuelConsumedThisMonth,
    int     OutagesThisMonth,
    int     CurrentlyRunning,
    double  TotalRuntimeHoursAllTime
);

// ════════════════════════════════════════════════════════════════════════════
//  Diesel Record DTOs
// ════════════════════════════════════════════════════════════════════════════

public record CreateDieselRecordRequest(
    DateTime RecordDate,
    string   RecordType,
    double   QuantityLitres,
    decimal  UnitCostNaira,
    string?  Supplier,
    string?  Destination,
    string?  Notes
);

public record DieselRecordDto(
    Guid      Id,
    DateTime  RecordDate,
    string    RecordType,
    double    QuantityLitres,
    decimal   UnitCostNaira,
    decimal   TotalCostNaira,
    string?   Supplier,
    string?   Destination,
    string    RequestedByEmail,
    string    RequestedByName,
    string?   ApprovedByEmail,
    string?   ApprovedByName,
    DateTime? ApprovedAt,
    string?   Notes,
    DateTime  CreatedAt
);

public record DieselRecordListResponse(
    IReadOnlyList<DieselRecordDto> Items,
    int     TotalCount,
    int     Page,
    int     PageSize
);

public record DieselStatsDto(
    double  TotalPurchasedLitresThisMonth,
    double  TotalDispensedLitresThisMonth,
    decimal TotalSpendThisMonth,
    double  CurrentStockLitres,          // purchases − dispensed (simplified)
    double  TotalPurchasedLitresAllTime
);

// ── Combined fuel/power dashboard stats ───────────────────────────────────────
public record FuelPowerSummary(
    GeneratorStatsDto  Generator,
    DieselStatsDto     Diesel
);
