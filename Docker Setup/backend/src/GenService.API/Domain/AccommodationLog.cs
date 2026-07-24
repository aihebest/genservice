namespace GenService.API.Domain;

/// <summary>
/// Feeding &amp; Accommodation record for the staff guest house (staff on transit).
/// One record per guest stay. Reference format: FA/26/001
/// </summary>
public class AccommodationLog
{
    public Guid   Id        { get; set; } = Guid.NewGuid();
    public string Reference { get; set; } = "";   // FA/26/001

    // ── Guest ────────────────────────────────────────────────────────────────
    public string  GuestName  { get; set; } = "";   // staff name
    public string? Department { get; set; }          // department / unit
    public string  GuestHouse { get; set; } = "";    // which guest house / location
    public string? Purpose    { get; set; }          // reason / transit destination

    // ── Stay ─────────────────────────────────────────────────────────────────
    public DateOnly  CheckInDate  { get; set; }
    public DateOnly? CheckOutDate { get; set; }       // optional — open stay
    public int?      Nights       { get; set; }       // number of nights

    // ── Feeding ───────────────────────────────────────────────────────────────
    public string?  MealPlan          { get; set; }   // None | Breakfast | Half Board | Full Board
    public int?     NumberOfMeals     { get; set; }
    public decimal? FeedingCostNaira  { get; set; }

    // ── Accommodation ──────────────────────────────────────────────────────────
    public decimal? AccommodationCostNaira { get; set; }

    // ── Totals & status ────────────────────────────────────────────────────────
    public decimal? TotalCostNaira { get; set; }      // feeding + accommodation
    public string   Status         { get; set; } = AccommodationStatus.CheckedIn; // Reserved | CheckedIn | CheckedOut

    public string?  Notes { get; set; }

    // ── Logger ──────────────────────────────────────────────────────────────────
    public string   LoggedByEmail { get; set; } = "";
    public string   LoggedByName  { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class AccommodationStatus
{
    public const string Reserved   = "Reserved";
    public const string CheckedIn  = "CheckedIn";
    public const string CheckedOut = "CheckedOut";
}

public static class MealPlanValue
{
    public const string None      = "None";
    public const string Breakfast = "Breakfast";
    public const string HalfBoard = "Half Board";
    public const string FullBoard = "Full Board";
}
