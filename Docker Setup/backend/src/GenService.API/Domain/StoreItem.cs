namespace GenService.API.Domain;

/// <summary>
/// Master inventory item in the General Service store.
/// </summary>
public class StoreItem
{
    public Guid    Id          { get; set; } = Guid.NewGuid();

    /// <summary>Auto-generated register number, e.g. SI/26/001</summary>
    public string  ItemCode    { get; set; } = "";

    public string  Name        { get; set; } = "";
    public string  Category    { get; set; } = "";

    /// <summary>e.g. Pieces | Litres | Metres | Kg | Rolls | Packets | Pairs | Sets</summary>
    public string  Unit        { get; set; } = "Pieces";

    public double  QuantityInStock { get; set; } = 0;

    /// <summary>Alert when stock falls to or below this level.</summary>
    public double  ReorderLevel    { get; set; } = 0;

    public decimal UnitCostNaira  { get; set; } = 0;

    public string? Description    { get; set; }

    /// <summary>Physical location of the item, e.g. "Store Room A — Shelf 3"</summary>
    public string? StoreLocation  { get; set; }

    public string? Supplier       { get; set; }

    public bool    IsActive       { get; set; } = true;

    public string  CreatedByEmail { get; set; } = "";
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt     { get; set; } = DateTime.UtcNow;
}

public static class StoreItemCategory
{
    public const string GeneratorParts    = "Generator Parts";
    public const string ACParts          = "AC Parts";
    public const string ElectricalItems  = "Electrical Items";
    public const string PlumbingItems    = "Plumbing Items";
    public const string CleaningSupplies = "Cleaning Supplies";
    public const string Lubricants       = "Lubricants & Oils";
    public const string VehicleParts     = "Vehicle Parts";
    public const string SafetyItems      = "Safety Items";
    public const string OfficeSupplies   = "Office Supplies";
    public const string GeneralStore     = "General Store";

    public static readonly string[] All =
    [
        GeneratorParts, ACParts, ElectricalItems, PlumbingItems,
        CleaningSupplies, Lubricants, VehicleParts, SafetyItems,
        OfficeSupplies, GeneralStore,
    ];
}

public static class StoreItemUnit
{
    public static readonly string[] All =
    [
        "Pieces", "Litres", "Metres", "Kg", "Rolls",
        "Packets", "Pairs", "Sets", "Boxes", "Cartons",
        "Gallons", "Bags", "Drums",
    ];
}
