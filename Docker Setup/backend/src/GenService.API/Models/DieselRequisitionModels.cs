namespace GenService.API.Models;

// ── Queries ───────────────────────────────────────────────────────────────────

public record DieselRequisitionQuery(
    string? Status       = null,
    string? EquipType    = null,
    string? Location     = null,
    string? Requester    = null,
    string? From         = null,
    string? To           = null,
    int     Page         = 1,
    int     PageSize     = 30
);

// ── Request bodies ────────────────────────────────────────────────────────────

public record CreateDieselRequisitionRequest(
    string  Purpose,
    string  EquipmentType,
    string? EquipmentReference,
    string  Location,
    double  QuantityRequestedLitres,
    string? Notes
);

public record ApproveDieselRequisitionRequest(
    string? Notes = null
);

public record RejectDieselRequisitionRequest(
    string Reason
);

public record DispenseDieselRequest(
    double   QuantityDispensedLitres,
    double   TankLevelBeforeLitres,
    decimal  UnitCostPerLitreNaira,
    string?  Notes = null
);

// ── DTOs ──────────────────────────────────────────────────────────────────────

public class DieselRequisitionDto
{
    public Guid     Id                       { get; init; }
    public string   RequisitionNumber        { get; init; } = "";
    public string   Purpose                  { get; init; } = "";
    public string   EquipmentType            { get; init; } = "";
    public string?  EquipmentReference       { get; init; }
    public string   Location                 { get; init; } = "";
    public double   QuantityRequestedLitres  { get; init; }
    public string   RequestedByEmail         { get; init; } = "";
    public string   RequestedByName          { get; init; } = "";
    public string   Department               { get; init; } = "";
    public string   Status                   { get; init; } = "";

    // Approval
    public string?   ApprovedByName          { get; init; }
    public DateTime? ApprovedAt              { get; init; }

    // Rejection
    public string?   RejectedByEmail         { get; init; }
    public string?   RejectionReason         { get; init; }
    public DateTime? RejectedAt              { get; init; }

    // Dispensing
    public string?   DispensedByName         { get; init; }
    public DateTime? DispensedAt             { get; init; }
    public double?   QuantityDispensedLitres { get; init; }
    public double?   TankLevelBeforeLitres   { get; init; }
    public double?   TankLevelAfterLitres    { get; init; }
    public decimal?  UnitCostPerLitreNaira   { get; init; }
    public decimal?  TotalCostNaira          { get; init; }
    public Guid?     LinkedDieselRecordId    { get; init; }

    public string?  Notes                    { get; init; }
    public DateTime CreatedAt                { get; init; }
    public DateTime UpdatedAt                { get; init; }
}

public class DieselRequisitionListResponse
{
    public List<DieselRequisitionDto> Items         { get; init; } = [];
    public int   Total                              { get; init; }
    public int   Page                               { get; init; }
    public int   PageSize                           { get; init; }
    public int   TotalPages                         { get; init; }
    public int   PendingCount                       { get; init; }
    public int   ApprovedCount                      { get; init; }
    public double TotalDispensedLitresThisMonth     { get; init; }
    public decimal TotalCostThisMonth               { get; init; }
}
