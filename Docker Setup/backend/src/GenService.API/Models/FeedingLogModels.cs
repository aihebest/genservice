namespace GenService.API.Models;

// ── Inbound ───────────────────────────────────────────────────────────────────

public record CreateFeedingLogRequest(
    string  EntryDate,              // YYYY-MM-DD
    string  StaffName,
    string? ProjectCostCode = null,
    int     Breakfast       = 0,
    int     SoftDrink       = 0,
    int     Water           = 0,
    int     Juice           = 0,
    int     Lunch           = 0,
    int     Dinner          = 0,
    int     Tea             = 0,
    int     Snacks          = 0,
    string? Notes           = null
);

public record FeedingLogQuery(
    string? From         = null,
    string? To           = null,
    string? CostCode     = null,
    string? Search       = null,
    int     Page         = 1,
    int     PageSize     = 50
);

public record UpdateMealRateItem(string ItemKey, decimal UnitPriceNaira);
public record UpdateMealRatesRequest(List<UpdateMealRateItem> Rates);

// ── Outbound ──────────────────────────────────────────────────────────────────

public record FeedingLogDto(
    Guid      Id,
    string    EntryDate,
    string    StaffName,
    string?   ProjectCostCode,
    int       Breakfast,
    int       SoftDrink,
    int       Water,
    int       Juice,
    int       Lunch,
    int       Dinner,
    int       Tea,
    int       Snacks,
    int       TotalItems,
    decimal   TotalCostNaira,
    string?   Notes,
    string    LoggedByName,
    DateTime  CreatedAt,
    string?   LastEditedByName,
    DateTime? LastEditedAt
);

public record FeedingLogListResponse(
    IEnumerable<FeedingLogDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    decimal TotalCostNaira      // total across the whole filtered set, not just this page
);

public record MealRateDto(string ItemKey, string Label, decimal UnitPriceNaira, int SortOrder);

/// <summary>One row of the "Summary by Cost Centre" / "Summary by Head Count" tables.</summary>
public record FeedingSummaryRow(
    string  Key,                 // cost code, or staff name
    int     Breakfast,
    int     SoftDrink,
    int     Water,
    int     Juice,
    int     Lunch,
    int     Dinner,
    int     Tea,
    int     Snacks,
    decimal TotalCostNaira
);

public record FeedingSummaryResponse(
    IEnumerable<FeedingSummaryRow> ByCostCentre,
    IEnumerable<FeedingSummaryRow> ByHeadCount,
    FeedingSummaryRow              GrandTotal,
    string                         PeriodLabel
);

public record FeedingStatsDto(
    int     EntriesThisMonth,
    int     StaffFedThisMonth,
    int     MealsThisMonth,
    decimal CostThisMonth
);
