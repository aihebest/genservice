namespace GenService.API.Domain;

/// <summary>
/// Immutable audit record of every stock quantity change.
/// Written automatically by the store controllers — never created directly.
/// </summary>
public class StoreMovement
{
    public Guid     Id               { get; set; } = Guid.NewGuid();
    public Guid     StoreItemId      { get; set; }

    /// <summary>ItemCode snapshot for quick queries without join.</summary>
    public string   ItemCode         { get; set; } = "";
    public string   ItemName         { get; set; } = "";

    /// <summary>Receipt | Issue | Adjustment | Return</summary>
    public string   MovementType     { get; set; } = "";

    public double   QuantityBefore   { get; set; }
    public double   QuantityChange   { get; set; }   // positive = in, negative = out
    public double   QuantityAfter    { get; set; }

    /// <summary>Requisition number, receipt number, or adjustment reference.</summary>
    public string?  Reference        { get; set; }

    public string?  Notes            { get; set; }

    public string   MovedByEmail     { get; set; } = "";
    public string   MovedByName      { get; set; } = "";

    public DateTime CreatedAt        { get; set; } = DateTime.UtcNow;

    // Navigation
    public StoreItem? StoreItem      { get; set; }
}

public static class StoreMovementType
{
    public const string Receipt    = "Receipt";    // items coming in
    public const string Issue      = "Issue";      // items going out (requisition)
    public const string Adjustment = "Adjustment"; // manual stock correction
    public const string Return     = "Return";     // items returned to store
}
