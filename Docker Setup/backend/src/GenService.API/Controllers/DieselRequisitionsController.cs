using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using System.Security.Claims;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/diesel/requisitions")]
[Authorize]
public class DieselRequisitionsController(
    GenServiceDbContext db,
    ILogger<DieselRequisitionsController> log) : ControllerBase
{
    private string UserEmail => User.FindFirstValue(ClaimTypes.Email)
                             ?? User.FindFirstValue("preferred_username")
                             ?? "unknown";
    private string UserName  => User.FindFirstValue(ClaimTypes.Name)
                             ?? User.FindFirstValue("name")
                             ?? UserEmail;
    private string UserRole  => User.FindFirstValue(ClaimTypes.Role)
                             ?? User.FindFirstValue("roles")
                             ?? "";

    private static readonly string[] PrivilegedRoles =
        ["SystemAdmin", "DepartmentManager", "Supervisor", "StoreOfficer"];

    private static readonly string[] ApproverRoles =
        ["SystemAdmin", "DepartmentManager", "Supervisor"];

    private static readonly string[] DispenserRoles =
        ["SystemAdmin", "DepartmentManager", "Supervisor", "StoreOfficer"];

    // ── GET /api/v1/diesel/requisitions ───────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<DieselRequisitionListResponse>> List(
        [FromQuery] DieselRequisitionQuery q)
    {
        var query = db.DieselRequisitions.AsQueryable();

        // Non-privileged users only see their own
        if (!PrivilegedRoles.Contains(UserRole))
            query = query.Where(r => r.RequestedByEmail == UserEmail);

        if (!string.IsNullOrWhiteSpace(q.Status))
            query = query.Where(r => r.Status == q.Status);

        if (!string.IsNullOrWhiteSpace(q.EquipType))
            query = query.Where(r => r.EquipmentType == q.EquipType);

        if (!string.IsNullOrWhiteSpace(q.Location))
            query = query.Where(r => r.Location.Contains(q.Location));

        if (!string.IsNullOrWhiteSpace(q.Requester))
        {
            var s = q.Requester.Trim().ToLower();
            query = query.Where(r =>
                r.RequestedByEmail.ToLower().Contains(s) ||
                r.RequestedByName.ToLower().Contains(s));
        }

        if (DateTime.TryParse(q.From, out var from)) query = query.Where(r => r.CreatedAt >= from);
        if (DateTime.TryParse(q.To,   out var to))   query = query.Where(r => r.CreatedAt <= to.AddDays(1));

        var total    = await query.CountAsync();
        var pending  = await query.CountAsync(r => r.Status == DieselRequisitionStatus.Pending);
        var approved = await query.CountAsync(r => r.Status == DieselRequisitionStatus.Approved);

        // Month-to-date totals from dispensed records
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var dispensed  = await db.DieselRequisitions
            .Where(r => r.Status == DieselRequisitionStatus.Dispensed && r.DispensedAt >= monthStart)
            .ToListAsync();

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return Ok(new DieselRequisitionListResponse
        {
            Items          = items.Select(ToDto).ToList(),
            Total          = total,
            Page           = q.Page,
            PageSize       = q.PageSize,
            TotalPages     = (int)Math.Ceiling(total / (double)q.PageSize),
            PendingCount   = pending,
            ApprovedCount  = approved,
            TotalDispensedLitresThisMonth = dispensed.Sum(r => r.QuantityDispensedLitres ?? 0),
            TotalCostThisMonth            = dispensed.Sum(r => r.TotalCostNaira ?? 0),
        });
    }

    // ── GET /api/v1/diesel/requisitions/{id} ──────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DieselRequisitionDto>> GetById(Guid id)
    {
        var req = await db.DieselRequisitions.FindAsync(id);
        if (req is null) return NotFound();

        if (!PrivilegedRoles.Contains(UserRole) && req.RequestedByEmail != UserEmail)
            return Forbid();

        return Ok(ToDto(req));
    }

    // ── POST /api/v1/diesel/requisitions ──────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<DieselRequisitionDto>> Create(
        [FromBody] CreateDieselRequisitionRequest body)
    {
        if (body.QuantityRequestedLitres <= 0)
            return BadRequest("Quantity must be greater than zero.");

        // Auto-number DR/YY/NNN
        var year = DateTime.UtcNow.Year.ToString()[2..];
        var last = await db.DieselRequisitions
            .Where(r => r.RequisitionNumber.StartsWith($"DR/{year}/"))
            .OrderByDescending(r => r.RequisitionNumber)
            .Select(r => r.RequisitionNumber)
            .FirstOrDefaultAsync();

        int next = 1;
        if (last is not null && int.TryParse(last.Split('/').Last(), out int n)) next = n + 1;
        var number = $"DR/{year}/{next:D3}";

        // Fetch dept from user profile
        var dbUser = await db.AppUsers
            .FirstOrDefaultAsync(u => u.Email.ToLower() == UserEmail.ToLower());

        var req = new DieselRequisition
        {
            RequisitionNumber       = number,
            Purpose                 = body.Purpose.Trim(),
            EquipmentType           = body.EquipmentType,
            EquipmentReference      = body.EquipmentReference?.Trim(),
            Location                = body.Location.Trim(),
            QuantityRequestedLitres = body.QuantityRequestedLitres,
            RequestedByEmail        = UserEmail,
            RequestedByName         = UserName,
            Department              = dbUser?.Department ?? "General Service",
            Status                  = DieselRequisitionStatus.Pending,
            Notes                   = body.Notes?.Trim(),
            CreatedAt               = DateTime.UtcNow,
            UpdatedAt               = DateTime.UtcNow,
        };

        db.DieselRequisitions.Add(req);
        await db.SaveChangesAsync();

        log.LogInformation("⛽ Diesel requisition {Number} submitted by {User}", number, UserEmail);
        return CreatedAtAction(nameof(GetById), new { id = req.Id }, ToDto(req));
    }

    // ── POST /api/v1/diesel/requisitions/{id}/approve ─────────────────────────
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "SystemAdmin,DepartmentManager,Supervisor")]
    public async Task<ActionResult<DieselRequisitionDto>> Approve(
        Guid id, [FromBody] ApproveDieselRequisitionRequest body)
    {
        var req = await db.DieselRequisitions.FindAsync(id);
        if (req is null) return NotFound();
        if (req.Status != DieselRequisitionStatus.Pending)
            return Conflict($"Requisition is already {req.Status}.");

        req.Status          = DieselRequisitionStatus.Approved;
        req.ApprovedByEmail = UserEmail;
        req.ApprovedByName  = UserName;
        req.ApprovedAt      = DateTime.UtcNow;
        req.Notes = body.Notes is not null
            ? (req.Notes is null ? body.Notes : $"{req.Notes}\n[Approval] {body.Notes}")
            : req.Notes;
        req.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        log.LogInformation("✅ Diesel requisition {Number} approved by {User}", req.RequisitionNumber, UserEmail);
        return Ok(ToDto(req));
    }

    // ── POST /api/v1/diesel/requisitions/{id}/reject ──────────────────────────
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "SystemAdmin,DepartmentManager,Supervisor")]
    public async Task<ActionResult<DieselRequisitionDto>> Reject(
        Guid id, [FromBody] RejectDieselRequisitionRequest body)
    {
        var req = await db.DieselRequisitions.FindAsync(id);
        if (req is null) return NotFound();
        if (req.Status != DieselRequisitionStatus.Pending)
            return Conflict($"Requisition is already {req.Status}.");

        req.Status          = DieselRequisitionStatus.Rejected;
        req.RejectedByEmail = UserEmail;
        req.RejectionReason = body.Reason.Trim();
        req.RejectedAt      = DateTime.UtcNow;
        req.UpdatedAt       = DateTime.UtcNow;

        await db.SaveChangesAsync();
        log.LogInformation("❌ Diesel requisition {Number} rejected by {User}", req.RequisitionNumber, UserEmail);
        return Ok(ToDto(req));
    }

    // ── POST /api/v1/diesel/requisitions/{id}/dispense ────────────────────────
    /// <summary>
    /// Records actual diesel dispensing.
    /// Automatically creates a linked DieselRecord for fuel report integration.
    /// Tank level before is supplied by the dispenser; after is calculated.
    /// </summary>
    [HttpPost("{id:guid}/dispense")]
    [Authorize(Roles = "SystemAdmin,DepartmentManager,Supervisor,StoreOfficer")]
    public async Task<ActionResult<DieselRequisitionDto>> Dispense(
        Guid id, [FromBody] DispenseDieselRequest body)
    {
        if (body.QuantityDispensedLitres <= 0) return BadRequest("Quantity must be greater than zero.");
        if (body.TankLevelBeforeLitres  < 0)  return BadRequest("Tank level cannot be negative.");

        var req = await db.DieselRequisitions.FindAsync(id);
        if (req is null) return NotFound();
        if (req.Status != DieselRequisitionStatus.Approved)
            return Conflict("Requisition must be Approved before it can be dispensed.");

        var totalCost = (decimal)body.QuantityDispensedLitres * body.UnitCostPerLitreNaira;
        var tankAfter = body.TankLevelBeforeLitres + body.QuantityDispensedLitres;

        req.Status                    = DieselRequisitionStatus.Dispensed;
        req.DispensedByEmail          = UserEmail;
        req.DispensedByName           = UserName;
        req.DispensedAt               = DateTime.UtcNow;
        req.QuantityDispensedLitres   = body.QuantityDispensedLitres;
        req.TankLevelBeforeLitres     = body.TankLevelBeforeLitres;
        req.TankLevelAfterLitres      = tankAfter;
        req.UnitCostPerLitreNaira     = body.UnitCostPerLitreNaira;
        req.TotalCostNaira            = totalCost;
        req.Notes = body.Notes is not null
            ? (req.Notes is null ? body.Notes : $"{req.Notes}\n[Dispense] {body.Notes}")
            : req.Notes;
        req.UpdatedAt = DateTime.UtcNow;

        // Auto-create a DieselRecord so the dispense appears in fuel reports
        var dieselRecord = new DieselRecord
        {
            RecordDate        = DateTime.UtcNow,
            RecordType        = DieselRecordType.Dispensed,
            QuantityLitres    = body.QuantityDispensedLitres,
            UnitCostNaira     = body.UnitCostPerLitreNaira,
            TotalCostNaira    = totalCost,
            Destination       = req.EquipmentReference ?? req.Location,
            RequestedByEmail  = req.RequestedByEmail,
            RequestedByName   = req.RequestedByName,
            ApprovedByEmail   = req.ApprovedByEmail,
            ApprovedByName    = req.ApprovedByName,
            ApprovedAt        = req.ApprovedAt,
            Notes             = $"Diesel requisition {req.RequisitionNumber} — {req.Purpose}",
            CreatedAt         = DateTime.UtcNow,
            UpdatedAt         = DateTime.UtcNow,
        };
        db.DieselRecords.Add(dieselRecord);
        await db.SaveChangesAsync();

        req.LinkedDieselRecordId = dieselRecord.Id;
        await db.SaveChangesAsync();

        log.LogInformation("⛽ Diesel requisition {Number} dispensed {Litres}L by {User}",
            req.RequisitionNumber, body.QuantityDispensedLitres, UserEmail);

        return Ok(ToDto(req));
    }

    // ── DELETE /api/v1/diesel/requisitions/{id} ───────────────────────────────
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var req = await db.DieselRequisitions.FindAsync(id);
        if (req is null) return NotFound();

        var isPrivileged = PrivilegedRoles.Contains(UserRole);
        if (!isPrivileged && req.RequestedByEmail != UserEmail)
            return Forbid();

        if (req.Status != DieselRequisitionStatus.Pending)
            return Conflict("Only Pending requisitions can be deleted.");

        db.DieselRequisitions.Remove(req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── GET /api/v1/diesel/requisitions/stats ─────────────────────────────────
    [HttpGet("stats")]
    [Authorize(Roles = "SystemAdmin,DepartmentManager,Supervisor")]
    public async Task<IActionResult> Stats()
    {
        var now        = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var yearStart  = new DateTime(now.Year, 1, 1);

        var all = await db.DieselRequisitions.ToListAsync();

        var byStatus = all
            .GroupBy(r => r.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        var byEquipType = all
            .GroupBy(r => r.EquipmentType)
            .Select(g => new { type = g.Key, count = g.Count(), litres = g.Sum(r => r.QuantityDispensedLitres ?? 0) })
            .ToList();

        // Monthly trend (last 6 months)
        var monthlyTrend = Enumerable.Range(0, 6)
            .Select(i => now.AddMonths(-i))
            .Select(m => new
            {
                month   = m.ToString("MMM yyyy"),
                litres  = all.Where(r => r.Status == DieselRequisitionStatus.Dispensed
                              && r.DispensedAt.HasValue
                              && r.DispensedAt.Value.Year  == m.Year
                              && r.DispensedAt.Value.Month == m.Month)
                             .Sum(r => r.QuantityDispensedLitres ?? 0),
                cost    = all.Where(r => r.Status == DieselRequisitionStatus.Dispensed
                              && r.DispensedAt.HasValue
                              && r.DispensedAt.Value.Year  == m.Year
                              && r.DispensedAt.Value.Month == m.Month)
                             .Sum(r => r.TotalCostNaira ?? 0),
            })
            .OrderBy(x => x.month)
            .ToList();

        return Ok(new
        {
            totalAllTime       = all.Count,
            pendingCount       = byStatus.GetValueOrDefault("Pending",   0),
            approvedCount      = byStatus.GetValueOrDefault("Approved",  0),
            dispensedCount     = byStatus.GetValueOrDefault("Dispensed", 0),
            rejectedCount      = byStatus.GetValueOrDefault("Rejected",  0),
            litresThisMonth    = all.Where(r => r.Status == DieselRequisitionStatus.Dispensed && r.DispensedAt >= monthStart)
                                    .Sum(r => r.QuantityDispensedLitres ?? 0),
            costThisMonth      = all.Where(r => r.Status == DieselRequisitionStatus.Dispensed && r.DispensedAt >= monthStart)
                                    .Sum(r => r.TotalCostNaira ?? 0),
            litresThisYear     = all.Where(r => r.Status == DieselRequisitionStatus.Dispensed && r.DispensedAt >= yearStart)
                                    .Sum(r => r.QuantityDispensedLitres ?? 0),
            costThisYear       = all.Where(r => r.Status == DieselRequisitionStatus.Dispensed && r.DispensedAt >= yearStart)
                                    .Sum(r => r.TotalCostNaira ?? 0),
            byEquipmentType    = byEquipType,
            monthlyTrend       = monthlyTrend,
        });
    }

    // ── Projection ────────────────────────────────────────────────────────────
    private static DieselRequisitionDto ToDto(DieselRequisition r) => new()
    {
        Id                       = r.Id,
        RequisitionNumber        = r.RequisitionNumber,
        Purpose                  = r.Purpose,
        EquipmentType            = r.EquipmentType,
        EquipmentReference       = r.EquipmentReference,
        Location                 = r.Location,
        QuantityRequestedLitres  = r.QuantityRequestedLitres,
        RequestedByEmail         = r.RequestedByEmail,
        RequestedByName          = r.RequestedByName,
        Department               = r.Department,
        Status                   = r.Status,
        ApprovedByName           = r.ApprovedByName,
        ApprovedAt               = r.ApprovedAt,
        RejectedByEmail          = r.RejectedByEmail,
        RejectionReason          = r.RejectionReason,
        RejectedAt               = r.RejectedAt,
        DispensedByName          = r.DispensedByName,
        DispensedAt              = r.DispensedAt,
        QuantityDispensedLitres  = r.QuantityDispensedLitres,
        TankLevelBeforeLitres    = r.TankLevelBeforeLitres,
        TankLevelAfterLitres     = r.TankLevelAfterLitres,
        UnitCostPerLitreNaira    = r.UnitCostPerLitreNaira,
        TotalCostNaira           = r.TotalCostNaira,
        LinkedDieselRecordId     = r.LinkedDieselRecordId,
        Notes                    = r.Notes,
        CreatedAt                = r.CreatedAt,
        UpdatedAt                = r.UpdatedAt,
    };
}
