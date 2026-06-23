namespace GenService.API.Domain;

/// <summary>
/// A request to withdraw one or more items from the General Service store.
/// Status flow: Pending → Approved → Issued
///                      → Rejected
/// </summary>
public class StoreRequisition
{
    public Guid   Id                  { get; set; } = Guid.NewGuid();

    /// <summary>Auto-generated, e.g. SR/26/001</summary>
    public string RequisitionNumber   { get; set; } = "";

    public string RequestedByEmail    { get; set; } = "";
    public string RequestedByName     { get; set; } = "";
    public string Department          { get; set; } = "";

    /// <summary>What the items will be used for.</summary>
    public string Purpose             { get; set; } = "";

    /// <summary>Optional link to a service request or maintenance job.</summary>
    public string? LinkedReference    { get; set; }

    public string Status              { get; set; } = StoreRequisitionStatus.Pending;

    // Approval
    public string?   ApprovedByEmail  { get; set; }
    public string?   ApprovedByName   { get; set; }
    public DateTime? ApprovedAt       { get; set; }

    // Rejection
    public string?   RejectedByEmail  { get; set; }
    public string?   RejectionReason  { get; set; }
    public DateTime? RejectedAt       { get; set; }

    // Issuance (StoreOfficer)
    public string?   IssuedByEmail    { get; set; }
    public string?   IssuedByName     { get; set; }
    public DateTime? IssuedAt         { get; set; }

    public string?   Notes            { get; set; }

    public DateTime CreatedAt         { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt         { get; set; } = DateTime.UtcNow;

    // Navigation
    public List<StoreRequisitionItem> Items { get; set; } = [];
}

public static class StoreRequisitionStatus
{
    public const string Pending  = "Pending";
    public const string Approved = "Approved";
    public const string Issued   = "Issued";
    public const string Rejected = "Rejected";
}

/// <summary>A single line item within a store requisition.</summary>
public class StoreRequisitionItem
{
    public Guid    Id                  { get; set; } = Guid.NewGuid();
    public Guid    RequisitionId       { get; set; }
    public Guid    StoreItemId         { get; set; }

    /// <summary>Snapshot of item name at time of requisition.</summary>
    public string  ItemName            { get; set; } = "";
    public string  Unit                { get; set; } = "";

    public double  QuantityRequested   { get; set; }

    /// <summary>Actual quantity issued (may differ from requested if partial issue).</summary>
    public double  QuantityIssued      { get; set; } = 0;

    public decimal UnitCostNaira       { get; set; } = 0;

    // Navigation
    public StoreRequisition? Requisition { get; set; }
    public StoreItem?         StoreItem  { get; set; }
}
