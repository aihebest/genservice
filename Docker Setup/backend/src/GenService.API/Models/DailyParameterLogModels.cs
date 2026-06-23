namespace GenService.API.Models;

// ── Query params ──────────────────────────────────────────────────────────────
public record DailyParameterLogQuery(
    string? Location = null,
    string? From     = null,   // ISO date
    string? To       = null,   // ISO date
    int     Page     = 1,
    int     PageSize = 20
);

// ── Request payloads ──────────────────────────────────────────────────────────
public record CreateDailyParameterLogRequest(
    string   LogDate,          // "YYYY-MM-DD"
    string   Location,

    // Power
    double?  NepaHoursAvailable,
    double?  GeneratorHoursRun,
    double?  DieselConsumedLitres,
    double?  DieselBalanceLitres,
    string?  GeneratorStatus,
    double?  GeneratorRunHourMeter,

    // Water
    string?  WaterSource,
    double?  WaterTankLevelPercent,
    string?  WaterStatus,

    // Staff
    int?     StaffPresent,
    int?     ExpectedStaff,
    int?     VisitorCount,

    // Facility checks
    bool     CleaningDone,
    bool     WasteDisposed,
    string?  SecurityStatus,

    // Observations
    string?  MaintenanceIssues,
    string?  ActionsTaken,
    string?  PendingActions,
    string?  GeneralRemarks
);

public record UpdateDailyParameterLogRequest(
    // Power
    double?  NepaHoursAvailable    = null,
    double?  GeneratorHoursRun     = null,
    double?  DieselConsumedLitres  = null,
    double?  DieselBalanceLitres   = null,
    string?  GeneratorStatus       = null,
    double?  GeneratorRunHourMeter = null,
    // Water
    string?  WaterSource           = null,
    double?  WaterTankLevelPercent = null,
    string?  WaterStatus           = null,
    // Staff
    int?     StaffPresent          = null,
    int?     ExpectedStaff         = null,
    int?     VisitorCount          = null,
    // Facility
    bool?    CleaningDone          = null,
    bool?    WasteDisposed         = null,
    string?  SecurityStatus        = null,
    // Observations
    string?  MaintenanceIssues     = null,
    string?  ActionsTaken          = null,
    string?  PendingActions        = null,
    string?  GeneralRemarks        = null
);

// ── DTO ───────────────────────────────────────────────────────────────────────
public record DailyParameterLogDto(
    Guid    Id,
    string  LogDate,         // "YYYY-MM-DD"
    string  Location,
    // Power
    double? NepaHoursAvailable,
    double? GeneratorHoursRun,
    double? DieselConsumedLitres,
    double? DieselBalanceLitres,
    string? GeneratorStatus,
    double? GeneratorRunHourMeter,
    // Water
    string? WaterSource,
    double? WaterTankLevelPercent,
    string? WaterStatus,
    // Staff
    int?    StaffPresent,
    int?    ExpectedStaff,
    int?    VisitorCount,
    // Facility
    bool    CleaningDone,
    bool    WasteDisposed,
    string? SecurityStatus,
    // Observations
    string? MaintenanceIssues,
    string? ActionsTaken,
    string? PendingActions,
    string? GeneralRemarks,
    // Logger
    string  LoggedByEmail,
    string  LoggedByName,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

// ── List response ─────────────────────────────────────────────────────────────
public record DailyParameterLogListResponse(
    IEnumerable<DailyParameterLogDto> Items,
    int  TotalCount,
    int  Page,
    int  PageSize
);

// ── Stats ─────────────────────────────────────────────────────────────────────
public record DailyParameterLogStatsDto(
    int     LogsThisMonth,
    double? AvgNepaHoursThisMonth,
    double? AvgGeneratorHoursThisMonth,
    double? TotalDieselThisMonth,
    int     LocationsLogged
);
