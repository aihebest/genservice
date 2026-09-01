namespace GenService.API.Domain;

/// <summary>
/// Daily feeding log for staff at the guest house — one row per person per day,
/// mirroring the "JAYVIK FEEDING LOG" spreadsheet the General Service team used
/// previously. Quantities are per meal item; the money value is computed from the
/// meal rate table at the time of entry and stored, so later rate changes never
/// alter historical records.
/// </summary>
public class FeedingLogEntry
{
    public Guid     Id        { get; set; } = Guid.NewGuid();
    public DateOnly EntryDate { get; set; }

    /// <summary>Staff name, or "Extra" for meals not attributable to one person.</summary>
    public string  StaffName       { get; set; } = "";
    /// <summary>Project number / cost code the meals are charged to (e.g. 1404, 150006).</summary>
    public string? ProjectCostCode { get; set; }

    // ── Meal item quantities ──────────────────────────────────────────────────
    public int Breakfast { get; set; }
    public int SoftDrink { get; set; }
    public int Water     { get; set; }
    public int Juice     { get; set; }
    public int Lunch     { get; set; }
    public int Dinner    { get; set; }
    public int Tea       { get; set; }
    public int Snacks    { get; set; }

    /// <summary>Total value of this row, priced at entry time (snapshot).</summary>
    public decimal TotalCostNaira { get; set; }

    public string? Notes { get; set; }

    // ── Audit ─────────────────────────────────────────────────────────────────
    public string   LoggedByEmail { get; set; } = "";
    public string   LoggedByName  { get; set; } = "";
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt     { get; set; } = DateTime.UtcNow;
    public string?   LastEditedByName { get; set; }
    public DateTime? LastEditedAt     { get; set; }

    /// <summary>Total number of individual items on this row (useful for headcount reports).</summary>
    public int TotalItems => Breakfast + SoftDrink + Water + Juice + Lunch + Dinner + Tea + Snacks;
}

/// <summary>
/// Unit price per meal item. Editable by managers so the caterer's price changes
/// don't require a code change. Seeded with the rates in use at Aug 2026.
/// </summary>
public class MealRate
{
    public Guid    Id            { get; set; } = Guid.NewGuid();
    /// <summary>Stable key: Breakfast | SoftDrink | Water | Juice | Lunch | Dinner | Tea | Snacks</summary>
    public string  ItemKey       { get; set; } = "";
    public string  Label         { get; set; } = "";
    public decimal UnitPriceNaira{ get; set; }
    public int     SortOrder     { get; set; }

    public DateTime  UpdatedAt        { get; set; } = DateTime.UtcNow;
    public string?   LastEditedByName { get; set; }
    public DateTime? LastEditedAt     { get; set; }
}

/// <summary>Canonical meal item keys and the rates in force when the module was built.</summary>
public static class MealItems
{
    public const string Breakfast = "Breakfast";
    public const string SoftDrink = "SoftDrink";
    public const string Water     = "Water";
    public const string Juice     = "Juice";
    public const string Lunch     = "Lunch";
    public const string Dinner    = "Dinner";
    public const string Tea       = "Tea";
    public const string Snacks    = "Snacks";

    /// <summary>(key, label, price, order) — verified against the team's Aug-26 cost-centre totals.</summary>
    public static readonly (string Key, string Label, decimal Price, int Order)[] Defaults =
    [
        (Breakfast, "Breakfast",  6400m, 1),
        (SoftDrink, "Soft Drink",  500m, 2),
        (Water,     "Water",       400m, 3),
        (Juice,     "Juice",      2500m, 4),
        (Lunch,     "Lunch",      9600m, 5),
        (Dinner,    "Dinner",     9600m, 6),
        (Tea,       "Tea",         800m, 7),
        (Snacks,    "Snacks",     3000m, 8),
    ];
}
