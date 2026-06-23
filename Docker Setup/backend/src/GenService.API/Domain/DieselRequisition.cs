namespace GenService.API.Domain;

/// <summary>
/// Formal diesel requisition workflow.
/// Status flow: Pending → Approved → Dispensed
///                      → Rejected
///
/// When status moves to Dispensed, a linked DieselRecord (type = Dispensed)
/// is created automatically to integrate with the existing fuel reports.
/// </summary>
public class DieselRequisition
{
    public Guid   Id                  { get; set; } = Guid.NewGuid();

    /// <summary>Auto-generated — e.g. DR/26/001</summary>
    public string RequisitionNumber   { get; set; } = "";

    // ── Request details ──────────────────────────────────────────────────────
    public string  Purpose              { get; set; } = "";
    public string  EquipmentType        { get; set; } = DieselEquipmentType.Generator;
    public string? EquipmentReference   { get; set; }   // Asset No, Vehicle Reg, etc.
    public string  Location             { get; set; } = "";
    public double  QuantityRequestedLitres { get; set; }

    // ── Requester ────────────────────────────────────────────────────────────
    public string  RequestedByEmail    { get; set; } = "";
    public string  RequestedByName     { get; set; } = "";
    public string  Department          { get; set; } = "General Service";

    // ── Status ───────────────────────────────────────────────────────────────
    public string  Status              { get; set; } = DieselRequisitionStatus.Pending;

    // ── Approval ─────────────────────────────────────────────────────────────
    public string?   ApprovedByEmail   { get; set; }
    public string?   ApprovedByName    { get; set; }
    public DateTime? ApprovedAt        { get; set; }

    // ── Rejection ────────────────────────────────────────────────────────────
    public string?   RejectedByEmail   { get; set; }
    public string?   RejectionReason   { get; set; }
    public DateTime? RejectedAt        { get; set; }

    // ── Dispensing ───────────────────────────────────────────────────────────
    public string?   DispensedByEmail       { get; set; }
    public string?   DispensedByName        { get; set; }
    public DateTime? DispensedAt            { get; set; }
    public double?   QuantityDispensedLitres { get; set; }   // actual (may differ from requested)
    public double?   TankLevelBeforeLitres  { get; set; }    // snapshot before dispensing
    public double?   TankLevelAfterLitres   { get; set; }    // calculated after
    public decimal?  UnitCostPerLitreNaira  { get; set; }
    public decimal?  TotalCostNaira         { get; set; }

    // ── Back-link to DieselRecord created on dispense ─────────────────────────
    public Guid?     LinkedDieselRecordId   { get; set; }

    public string?   Notes              { get; set; }
    public DateTime  CreatedAt          { get; set; } = DateTime.UtcNow;
    public DateTime  UpdatedAt          { get; set; } = DateTime.UtcNow;
}

public static class DieselRequisitionStatus
{
    public const string Pending   = "Pending";
    public const string Approved  = "Approved";
    public const string Dispensed = "Dispensed";
    public const string Rejected  = "Rejected";
}

public static class DieselEquipmentType
{
    public const string Generator  = "Generator";
    public const string Vehicle    = "Vehicle";
    public const string FuelStore  = "Fuel Store";
    public const string Other      = "Other";

    public static readonly string[] All = [Generator, Vehicle, FuelStore, Other];
}
