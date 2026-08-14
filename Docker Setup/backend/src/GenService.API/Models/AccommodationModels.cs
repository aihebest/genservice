namespace GenService.API.Models;

// ── Inbound ───────────────────────────────────────────────────────────────────

public record CreateAccommodationRequest(
    string    GuestName,
    string    GuestHouse,
    string    CheckInDate,               // YYYY-MM-DD
    string?   Department             = null,
    string?   Purpose                = null,
    string?   CheckOutDate           = null, // YYYY-MM-DD
    int?      Nights                 = null,
    string?   MealPlan               = null,
    int?      NumberOfMeals          = null,
    decimal?  FeedingCostNaira       = null,
    decimal?  AccommodationCostNaira = null,
    string?   Status                 = null,
    string?   Notes                  = null
);

public record UpdateAccommodationRequest(
    string?   GuestName              = null,
    string?   GuestHouse             = null,
    string?   Department             = null,
    string?   Purpose                = null,
    string?   CheckInDate            = null,
    string?   CheckOutDate           = null,
    int?      Nights                 = null,
    string?   MealPlan               = null,
    int?      NumberOfMeals          = null,
    decimal?  FeedingCostNaira       = null,
    decimal?  AccommodationCostNaira = null,
    string?   Status                 = null,
    string?   Notes                  = null
);

public record AccommodationQuery(
    string? GuestHouse = null,
    string? Status     = null,
    string? Search     = null,
    string? From       = null,
    string? To         = null,
    int     Page       = 1,
    int     PageSize   = 20
);

// ── Outbound ──────────────────────────────────────────────────────────────────

public record AccommodationDto(
    Guid      Id,
    string    Reference,
    string    GuestName,
    string?   Department,
    string    GuestHouse,
    string?   Purpose,
    string    CheckInDate,
    string?   CheckOutDate,
    int?      Nights,
    string?   MealPlan,
    int?      NumberOfMeals,
    decimal?  FeedingCostNaira,
    decimal?  AccommodationCostNaira,
    decimal?  TotalCostNaira,
    string    Status,
    string?   Notes,
    string    LoggedByEmail,
    string    LoggedByName,
    DateTime  CreatedAt,
    DateTime  UpdatedAt,
    string?   LastEditedByName = null,
    DateTime? LastEditedAt     = null
);

public record AccommodationStatsDto(
    int      GuestsThisMonth,
    int      CurrentlyCheckedIn,
    decimal  FeedingCostThisMonth,
    decimal  AccommodationCostThisMonth,
    decimal  TotalCostThisMonth
);

public record AccommodationListResponse(
    IEnumerable<AccommodationDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
