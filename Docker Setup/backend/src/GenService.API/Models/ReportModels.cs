namespace GenService.API.Models;

// ── Shared ────────────────────────────────────────────────────────────────────

public record PeriodBreakdownItem(string Label, int Count);
public record TrendPoint(string Date, double Value);

// ── Request Report ────────────────────────────────────────────────────────────

public record RequestReportDto(
    // KPIs
    int    TotalRequests,
    int    OpenRequests,
    int    CompletedRequests,
    int    PendingApproval,
    int    RejectedRequests,
    double CompletionRatePercent,     // completed / (completed+rejected+cancelled) * 100

    // Breakdowns
    IReadOnlyList<PeriodBreakdownItem> ByCategory,
    IReadOnlyList<PeriodBreakdownItem> ByStatus,
    IReadOnlyList<PeriodBreakdownItem> ByPriority,

    // Daily submission trend (last N days)
    IReadOnlyList<TrendPoint> SubmissionTrend,

    // Top requesters
    IReadOnlyList<PeriodBreakdownItem> TopRequesters,

    string PeriodLabel
);

// ── Maintenance Report ────────────────────────────────────────────────────────

public record MaintenanceReportDto(
    // KPIs
    int    TotalSchedules,
    int    OverdueCount,
    int    CompletedThisPeriod,
    int    DueSoon,
    double ComplianceRatePercent,     // completed on time / total due * 100

    // Breakdowns
    IReadOnlyList<PeriodBreakdownItem> ByCategory,
    IReadOnlyList<PeriodBreakdownItem> ByFrequency,

    // Recently completed
    IReadOnlyList<MaintenanceCompletionItem> RecentCompletions,

    string PeriodLabel
);

public record MaintenanceCompletionItem(
    string    TaskName,
    string    Category,
    string    Location,
    DateTime  CompletedAt,
    string?   CompletedByName
);

// ── Fuel & Power Report ───────────────────────────────────────────────────────

public record FuelPowerReportDto(
    // Generator KPIs
    double TotalRuntimeHours,
    int    TotalOutages,
    double TotalFuelConsumedLitres,
    double AvgOutageDurationHours,
    int    CurrentlyRunning,

    // Diesel KPIs
    double  TotalPurchasedLitres,
    double  TotalDispensedLitres,
    decimal TotalSpendNaira,
    double  CurrentStockEstimateLitres,

    // Breakdowns
    IReadOnlyList<PeriodBreakdownItem> OutagesByReason,
    IReadOnlyList<PeriodBreakdownItem> DieselByType,

    // Trends
    IReadOnlyList<TrendPoint> RuntimeTrend,       // daily runtime hours
    IReadOnlyList<TrendPoint> DieselUsageTrend,   // daily diesel dispensed litres

    // Generator sessions
    IReadOnlyList<GeneratorSessionItem> RecentSessions,

    string PeriodLabel
);

public record GeneratorSessionItem(
    string    Location,
    string    RunReason,
    DateTime  StartTime,
    double?   RuntimeHours,
    double?   FuelConsumed,
    string?   OutageCause,
    string    Status
);
