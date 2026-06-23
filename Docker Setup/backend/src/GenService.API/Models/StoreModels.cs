namespace GenService.API.Models;

// ─── Store Items ──────────────────────────────────────────────────────────────

public record StoreItemQuery(
    string? Search      = null,
    string? Category    = null,
    bool?   LowStock    = null,   // true = only items at/below reorder level
    bool?   IsActive    = null,
    int     Page        = 1,
    int     PageSize    = 30
);

public record CreateStoreItemRequest(
    string  Name,
    string  Category,
    string  Unit,
    double  QuantityInStock,
    double  ReorderLevel,
    decimal UnitCostNaira,
    string? Description,
    string? StoreLocation,
    string? Supplier
);

public record UpdateStoreItemRequest(
    string  Name,
    string  Category,
    string  Unit,
    double  ReorderLevel,
    decimal UnitCostNaira,
    string? Description,
    string? StoreLocation,
    string? Supplier,
    bool    IsActive
);

public record RestockRequest(
    double  Quantity,
    decimal UnitCostNaira,
    string? Reference,
    string? Notes
);

public record AdjustStockRequest(
    double  NewQuantity,
    string  Reason
);

public class StoreItemDto
{
    public Guid     Id              { get; init; }
    public string   ItemCode        { get; init; } = "";
    public string   Name            { get; init; } = "";
    public string   Category        { get; init; } = "";
    public string   Unit            { get; init; } = "";
    public double   QuantityInStock { get; init; }
    public double   ReorderLevel    { get; init; }
    public bool     IsLowStock      { get; init; }
    public decimal  UnitCostNaira   { get; init; }
    public decimal  TotalValueNaira { get; init; }
    public string?  Description     { get; init; }
    public string?  StoreLocation   { get; init; }
    public string?  Supplier        { get; init; }
    public bool     IsActive        { get; init; }
    public string   CreatedByEmail  { get; init; } = "";
    public DateTime CreatedAt       { get; init; }
    public DateTime UpdatedAt       { get; init; }
}

public class StoreItemListResponse
{
    public List<StoreItemDto> Items      { get; init; } = [];
    public int                Total      { get; init; }
    public int                Page       { get; init; }
    public int                PageSize   { get; init; }
    public int                TotalPages { get; init; }
    public int                LowStockCount { get; init; }
    public decimal            TotalStoreValueNaira { get; init; }
}

public class StoreMovementDto
{
    public Guid     Id             { get; init; }
    public string   ItemCode       { get; init; } = "";
    public string   ItemName       { get; init; } = "";
    public string   MovementType   { get; init; } = "";
    public double   QuantityBefore { get; init; }
    public double   QuantityChange { get; init; }
    public double   QuantityAfter  { get; init; }
    public string?  Reference      { get; init; }
    public string?  Notes          { get; init; }
    public string   MovedByEmail   { get; init; } = "";
    public string   MovedByName    { get; init; } = "";
    public DateTime CreatedAt      { get; init; }
}

// ─── Store Requisitions ───────────────────────────────────────────────────────

public record StoreRequisitionQuery(
    string? Status     = null,
    string? Requester  = null,
    string? Department = null,
    int     Page       = 1,
    int     PageSize   = 30
);

public record CreateRequisitionLineItem(
    Guid   StoreItemId,
    double QuantityRequested
);

public record CreateStoreRequisitionRequest(
    string                         Purpose,
    string?                        LinkedReference,
    string?                        Notes,
    List<CreateRequisitionLineItem> Items
);

public record ApproveRequisitionRequest(
    string? Notes = null
);

public record RejectRequisitionRequest(
    string Reason
);

public record IssueRequisitionRequest(
    List<IssueLineItem> Items,
    string?             Notes
);

public record IssueLineItem(
    Guid   StoreRequisitionItemId,
    double QuantityIssued
);

public class StoreRequisitionItemDto
{
    public Guid    Id                  { get; init; }
    public Guid    StoreItemId         { get; init; }
    public string  ItemCode            { get; init; } = "";
    public string  ItemName            { get; init; } = "";
    public string  Unit                { get; init; } = "";
    public double  QuantityRequested   { get; init; }
    public double  QuantityIssued      { get; init; }
    public decimal UnitCostNaira       { get; init; }
    public decimal TotalCost           { get; init; }
    public double  CurrentStock        { get; init; }
}

public class StoreRequisitionDto
{
    public Guid      Id                  { get; init; }
    public string    RequisitionNumber   { get; init; } = "";
    public string    RequestedByEmail    { get; init; } = "";
    public string    RequestedByName     { get; init; } = "";
    public string    Department          { get; init; } = "";
    public string    Purpose             { get; init; } = "";
    public string?   LinkedReference     { get; init; }
    public string    Status              { get; init; } = "";
    public string?   ApprovedByName      { get; init; }
    public DateTime? ApprovedAt          { get; init; }
    public string?   RejectedByEmail     { get; init; }
    public string?   RejectionReason     { get; init; }
    public DateTime? RejectedAt          { get; init; }
    public string?   IssuedByName        { get; init; }
    public DateTime? IssuedAt            { get; init; }
    public string?   Notes               { get; init; }
    public DateTime  CreatedAt           { get; init; }
    public DateTime  UpdatedAt           { get; init; }
    public List<StoreRequisitionItemDto> Items { get; init; } = [];
    public decimal   TotalCostNaira      { get; init; }
}

public class StoreRequisitionListResponse
{
    public List<StoreRequisitionDto> Items      { get; init; } = [];
    public int                       Total      { get; init; }
    public int                       Page       { get; init; }
    public int                       PageSize   { get; init; }
    public int                       TotalPages { get; init; }
    public int                       PendingCount  { get; init; }
    public int                       ApprovedCount { get; init; }
}
