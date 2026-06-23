using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using System.Security.Claims;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/store/requisitions")]
[Authorize]
public class StoreRequisitionsController(GenServiceDbContext db, ILogger<StoreRequisitionsController> log) : ControllerBase
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

    // ── GET /api/v1/store/requisitions ────────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<StoreRequisitionListResponse>> List([FromQuery] StoreRequisitionQuery q)
    {
        var query = db.StoreRequisitions.Include(r => r.Items).ThenInclude(i => i.StoreItem).AsQueryable();

        // Non-managers/admins only see their own requisitions
        var isPrivileged = new[] { "SystemAdmin", "DepartmentManager", "Supervisor", "StoreOfficer" }
            .Contains(UserRole);

        if (!isPrivileged)
            query = query.Where(r => r.RequestedByEmail == UserEmail);

        if (!string.IsNullOrWhiteSpace(q.Status))
            query = query.Where(r => r.Status == q.Status);

        if (!string.IsNullOrWhiteSpace(q.Requester))
        {
            var s = q.Requester.Trim().ToLower();
            query = query.Where(r =>
                r.RequestedByEmail.ToLower().Contains(s) ||
                r.RequestedByName.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(q.Department))
            query = query.Where(r => r.Department == q.Department);

        var total     = await query.CountAsync();
        var pending   = await query.CountAsync(r => r.Status == StoreRequisitionStatus.Pending);
        var approved  = await query.CountAsync(r => r.Status == StoreRequisitionStatus.Approved);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return Ok(new StoreRequisitionListResponse
        {
            Items         = items.Select(ToDto).ToList(),
            Total         = total,
            Page          = q.Page,
            PageSize      = q.PageSize,
            TotalPages    = (int)Math.Ceiling(total / (double)q.PageSize),
            PendingCount  = pending,
            ApprovedCount = approved,
        });
    }

    // ── GET /api/v1/store/requisitions/{id} ───────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StoreRequisitionDto>> GetById(Guid id)
    {
        var req = await db.StoreRequisitions
            .Include(r => r.Items).ThenInclude(i => i.StoreItem)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (req is null) return NotFound();

        // Non-privileged users can only see their own
        var isPrivileged = new[] { "SystemAdmin", "DepartmentManager", "Supervisor", "StoreOfficer" }
            .Contains(UserRole);
        if (!isPrivileged && req.RequestedByEmail != UserEmail)
            return Forbid();

        return Ok(ToDto(req));
    }

    // ── POST /api/v1/store/requisitions ───────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<StoreRequisitionDto>> Create([FromBody] CreateStoreRequisitionRequest body)
    {
        if (body.Items is null || body.Items.Count == 0)
            return BadRequest("At least one item is required.");

        // Validate all items exist and are active
        var itemIds  = body.Items.Select(i => i.StoreItemId).ToList();
        var dbItems  = await db.StoreItems
            .Where(x => itemIds.Contains(x.Id) && x.IsActive)
            .ToDictionaryAsync(x => x.Id);

        var missing = itemIds.Except(dbItems.Keys).ToList();
        if (missing.Any())
            return BadRequest($"Item(s) not found or inactive: {string.Join(", ", missing)}");

        // Generate requisition number SR/YY/NNN
        var year = DateTime.UtcNow.Year.ToString()[2..];
        var last = await db.StoreRequisitions
            .Where(r => r.RequisitionNumber.StartsWith($"SR/{year}/"))
            .OrderByDescending(r => r.RequisitionNumber)
            .Select(r => r.RequisitionNumber)
            .FirstOrDefaultAsync();

        int next = 1;
        if (last is not null && int.TryParse(last.Split('/').Last(), out int n)) next = n + 1;
        var number = $"SR/{year}/{next:D3}";

        // Get user dept from DB
        var dbUser = await db.AppUsers.FirstOrDefaultAsync(u => u.Email.ToLower() == UserEmail.ToLower());

        var requisition = new StoreRequisition
        {
            RequisitionNumber = number,
            RequestedByEmail  = UserEmail,
            RequestedByName   = UserName,
            Department        = dbUser?.Department ?? "General Service",
            Purpose           = body.Purpose.Trim(),
            LinkedReference   = body.LinkedReference?.Trim(),
            Status            = StoreRequisitionStatus.Pending,
            Notes             = body.Notes?.Trim(),
            CreatedAt         = DateTime.UtcNow,
            UpdatedAt         = DateTime.UtcNow,
            Items             = body.Items.Select(li =>
            {
                var si = dbItems[li.StoreItemId];
                return new StoreRequisitionItem
                {
                    StoreItemId       = si.Id,
                    ItemName          = si.Name,
                    Unit              = si.Unit,
                    QuantityRequested = li.QuantityRequested,
                    QuantityIssued    = 0,
                    UnitCostNaira     = si.UnitCostNaira,
                };
            }).ToList(),
        };

        db.StoreRequisitions.Add(requisition);
        await db.SaveChangesAsync();

        log.LogInformation("📋 Requisition {Number} submitted by {User}", number, UserEmail);

        // Reload with navigation
        var created = await db.StoreRequisitions
            .Include(r => r.Items).ThenInclude(i => i.StoreItem)
            .FirstOrDefaultAsync(r => r.Id == requisition.Id);

        return CreatedAtAction(nameof(GetById), new { id = requisition.Id }, ToDto(created!));
    }

    // ── POST /api/v1/store/requisitions/{id}/approve ──────────────────────────
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "SystemAdmin,DepartmentManager,Supervisor")]
    public async Task<ActionResult<StoreRequisitionDto>> Approve(Guid id, [FromBody] ApproveRequisitionRequest body)
    {
        var req = await db.StoreRequisitions
            .Include(r => r.Items).ThenInclude(i => i.StoreItem)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (req is null) return NotFound();
        if (req.Status != StoreRequisitionStatus.Pending)
            return Conflict($"Requisition is already {req.Status}.");

        req.Status          = StoreRequisitionStatus.Approved;
        req.ApprovedByEmail = UserEmail;
        req.ApprovedByName  = UserName;
        req.ApprovedAt      = DateTime.UtcNow;
        req.Notes           = body.Notes is not null
                              ? (req.Notes is null ? body.Notes : $"{req.Notes}\n[Approval] {body.Notes}")
                              : req.Notes;
        req.UpdatedAt       = DateTime.UtcNow;

        await db.SaveChangesAsync();
        log.LogInformation("✅ Requisition {Number} approved by {User}", req.RequisitionNumber, UserEmail);
        return Ok(ToDto(req));
    }

    // ── POST /api/v1/store/requisitions/{id}/reject ───────────────────────────
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "SystemAdmin,DepartmentManager,Supervisor")]
    public async Task<ActionResult<StoreRequisitionDto>> Reject(Guid id, [FromBody] RejectRequisitionRequest body)
    {
        var req = await db.StoreRequisitions
            .Include(r => r.Items).ThenInclude(i => i.StoreItem)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (req is null) return NotFound();
        if (req.Status != StoreRequisitionStatus.Pending)
            return Conflict($"Requisition is already {req.Status}.");

        req.Status          = StoreRequisitionStatus.Rejected;
        req.RejectedByEmail = UserEmail;
        req.RejectionReason = body.Reason.Trim();
        req.RejectedAt      = DateTime.UtcNow;
        req.UpdatedAt       = DateTime.UtcNow;

        await db.SaveChangesAsync();
        log.LogInformation("❌ Requisition {Number} rejected by {User}", req.RequisitionNumber, UserEmail);
        return Ok(ToDto(req));
    }

    // ── POST /api/v1/store/requisitions/{id}/issue ────────────────────────────
    /// <summary>
    /// StoreOfficer physically issues items and decrements stock.
    /// Supports partial issue — each line item can be issued a different qty than requested.
    /// </summary>
    [HttpPost("{id:guid}/issue")]
    [Authorize(Roles = "SystemAdmin,StoreOfficer")]
    public async Task<ActionResult<StoreRequisitionDto>> Issue(Guid id, [FromBody] IssueRequisitionRequest body)
    {
        var req = await db.StoreRequisitions
            .Include(r => r.Items).ThenInclude(i => i.StoreItem)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (req is null) return NotFound();
        if (req.Status != StoreRequisitionStatus.Approved)
            return Conflict("Requisition must be Approved before it can be issued.");

        var lineMap = body.Items.ToDictionary(x => x.StoreRequisitionItemId);

        using var tx = await db.Database.BeginTransactionAsync();

        foreach (var line in req.Items)
        {
            if (!lineMap.TryGetValue(line.Id, out var issueLine)) continue;
            var qtyToIssue = issueLine.QuantityIssued;

            if (qtyToIssue < 0) return BadRequest("Issue quantity cannot be negative.");
            if (qtyToIssue > line.QuantityRequested)
                return BadRequest($"Cannot issue more than requested for {line.ItemName}.");

            var storeItem = await db.StoreItems.FindAsync(line.StoreItemId);
            if (storeItem is null) return BadRequest($"Store item not found: {line.StoreItemId}");

            if (storeItem.QuantityInStock < qtyToIssue)
                return BadRequest($"Insufficient stock for {storeItem.Name}. " +
                    $"Available: {storeItem.QuantityInStock} {storeItem.Unit}, requested: {qtyToIssue}.");

            var before = storeItem.QuantityInStock;
            storeItem.QuantityInStock -= qtyToIssue;
            storeItem.UpdatedAt        = DateTime.UtcNow;
            line.QuantityIssued        = qtyToIssue;

            db.StoreMovements.Add(new StoreMovement
            {
                StoreItemId    = storeItem.Id,
                ItemCode       = storeItem.ItemCode,
                ItemName       = storeItem.Name,
                MovementType   = StoreMovementType.Issue,
                QuantityBefore = before,
                QuantityChange = -qtyToIssue,
                QuantityAfter  = storeItem.QuantityInStock,
                Reference      = req.RequisitionNumber,
                Notes          = $"Issued against {req.RequisitionNumber} — {req.Purpose}",
                MovedByEmail   = UserEmail,
                MovedByName    = UserName,
                CreatedAt      = DateTime.UtcNow,
            });
        }

        req.Status        = StoreRequisitionStatus.Issued;
        req.IssuedByEmail = UserEmail;
        req.IssuedByName  = UserName;
        req.IssuedAt      = DateTime.UtcNow;
        req.Notes         = body.Notes is not null
                            ? (req.Notes is null ? body.Notes : $"{req.Notes}\n[Issue] {body.Notes}")
                            : req.Notes;
        req.UpdatedAt     = DateTime.UtcNow;

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        log.LogInformation("🏷️ Requisition {Number} issued by {User}", req.RequisitionNumber, UserEmail);
        return Ok(ToDto(req));
    }

    // ── DELETE /api/v1/store/requisitions/{id} ────────────────────────────────
    /// <summary>Only Pending requisitions can be deleted; only by the submitter or admin.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var req = await db.StoreRequisitions
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (req is null) return NotFound();

        var isAdmin = new[] { "SystemAdmin", "DepartmentManager" }.Contains(UserRole);
        if (!isAdmin && req.RequestedByEmail != UserEmail)
            return Forbid();

        if (req.Status != StoreRequisitionStatus.Pending)
            return Conflict("Only Pending requisitions can be deleted.");

        db.StoreRequisitions.Remove(req);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Projection ────────────────────────────────────────────────────────────
    private static StoreRequisitionDto ToDto(StoreRequisition r) => new()
    {
        Id                = r.Id,
        RequisitionNumber = r.RequisitionNumber,
        RequestedByEmail  = r.RequestedByEmail,
        RequestedByName   = r.RequestedByName,
        Department        = r.Department,
        Purpose           = r.Purpose,
        LinkedReference   = r.LinkedReference,
        Status            = r.Status,
        ApprovedByName    = r.ApprovedByName,
        ApprovedAt        = r.ApprovedAt,
        RejectedByEmail   = r.RejectedByEmail,
        RejectionReason   = r.RejectionReason,
        RejectedAt        = r.RejectedAt,
        IssuedByName      = r.IssuedByName,
        IssuedAt          = r.IssuedAt,
        Notes             = r.Notes,
        CreatedAt         = r.CreatedAt,
        UpdatedAt         = r.UpdatedAt,
        Items             = r.Items.Select(i => new StoreRequisitionItemDto
        {
            Id                = i.Id,
            StoreItemId       = i.StoreItemId,
            ItemCode          = i.StoreItem?.ItemCode ?? "",
            ItemName          = i.ItemName,
            Unit              = i.Unit,
            QuantityRequested = i.QuantityRequested,
            QuantityIssued    = i.QuantityIssued,
            UnitCostNaira     = i.UnitCostNaira,
            TotalCost         = (decimal)i.QuantityIssued * i.UnitCostNaira,
            CurrentStock      = i.StoreItem?.QuantityInStock ?? 0,
        }).ToList(),
        TotalCostNaira = r.Items.Sum(i => (decimal)i.QuantityIssued * i.UnitCostNaira),
    };
}
