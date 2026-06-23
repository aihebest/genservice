using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using System.Security.Claims;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/store/items")]
[Authorize]
public class StoreItemsController(GenServiceDbContext db, ILogger<StoreItemsController> log) : ControllerBase
{
    // ── Helper: current user ──────────────────────────────────────────────────
    private string UserEmail => User.FindFirstValue(ClaimTypes.Email)
                             ?? User.FindFirstValue("preferred_username")
                             ?? "unknown";
    private string UserName  => User.FindFirstValue(ClaimTypes.Name)
                             ?? User.FindFirstValue("name")
                             ?? UserEmail;

    // ── GET /api/v1/store/items ───────────────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<StoreItemListResponse>> List([FromQuery] StoreItemQuery q)
    {
        var query = db.StoreItems.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(x =>
                x.Name.ToLower().Contains(s) ||
                x.ItemCode.ToLower().Contains(s) ||
                (x.Description != null && x.Description.ToLower().Contains(s)) ||
                (x.Supplier    != null && x.Supplier.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(q.Category))
            query = query.Where(x => x.Category == q.Category);

        if (q.IsActive.HasValue)
            query = query.Where(x => x.IsActive == q.IsActive.Value);

        if (q.LowStock == true)
            query = query.Where(x => x.QuantityInStock <= x.ReorderLevel);

        var total     = await query.CountAsync();
        var lowCount  = await db.StoreItems.CountAsync(x => x.IsActive && x.QuantityInStock <= x.ReorderLevel);
        var storeVal  = await db.StoreItems.Where(x => x.IsActive).SumAsync(x => (decimal)x.QuantityInStock * x.UnitCostNaira);

        var items = await query
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Name)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return Ok(new StoreItemListResponse
        {
            Items      = items.Select(ToDto).ToList(),
            Total      = total,
            Page       = q.Page,
            PageSize   = q.PageSize,
            TotalPages = (int)Math.Ceiling(total / (double)q.PageSize),
            LowStockCount         = lowCount,
            TotalStoreValueNaira  = storeVal,
        });
    }

    // ── GET /api/v1/store/items/{id} ──────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StoreItemDto>> GetById(Guid id)
    {
        var item = await db.StoreItems.FindAsync(id);
        if (item is null) return NotFound();
        return Ok(ToDto(item));
    }

    // ── GET /api/v1/store/items/{id}/movements ────────────────────────────────
    [HttpGet("{id:guid}/movements")]
    public async Task<ActionResult<List<StoreMovementDto>>> Movements(Guid id, [FromQuery] int limit = 50)
    {
        var exists = await db.StoreItems.AnyAsync(x => x.Id == id);
        if (!exists) return NotFound();

        var movements = await db.StoreMovements
            .Where(x => x.StoreItemId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(m => new StoreMovementDto
            {
                Id             = m.Id,
                ItemCode       = m.ItemCode,
                ItemName       = m.ItemName,
                MovementType   = m.MovementType,
                QuantityBefore = m.QuantityBefore,
                QuantityChange = m.QuantityChange,
                QuantityAfter  = m.QuantityAfter,
                Reference      = m.Reference,
                Notes          = m.Notes,
                MovedByEmail   = m.MovedByEmail,
                MovedByName    = m.MovedByName,
                CreatedAt      = m.CreatedAt,
            })
            .ToListAsync();

        return Ok(movements);
    }

    // ── POST /api/v1/store/items ──────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "SystemAdmin,DepartmentManager,Supervisor,StoreOfficer")]
    public async Task<ActionResult<StoreItemDto>> Create([FromBody] CreateStoreItemRequest req)
    {
        // Generate item code SI/YY/NNN
        var year = DateTime.UtcNow.Year.ToString()[2..];
        var last = await db.StoreItems
            .Where(x => x.ItemCode.StartsWith($"SI/{year}/"))
            .OrderByDescending(x => x.ItemCode)
            .Select(x => x.ItemCode)
            .FirstOrDefaultAsync();

        int next = 1;
        if (last is not null && int.TryParse(last.Split('/').Last(), out int n)) next = n + 1;
        var code = $"SI/{year}/{next:D3}";

        var item = new StoreItem
        {
            ItemCode        = code,
            Name            = req.Name.Trim(),
            Category        = req.Category,
            Unit            = req.Unit,
            QuantityInStock = req.QuantityInStock,
            ReorderLevel    = req.ReorderLevel,
            UnitCostNaira   = req.UnitCostNaira,
            Description     = req.Description?.Trim(),
            StoreLocation   = req.StoreLocation?.Trim(),
            Supplier        = req.Supplier?.Trim(),
            IsActive        = true,
            CreatedByEmail  = UserEmail,
            CreatedAt       = DateTime.UtcNow,
            UpdatedAt       = DateTime.UtcNow,
        };

        db.StoreItems.Add(item);

        // Log initial stock as a Receipt movement if qty > 0
        if (req.QuantityInStock > 0)
        {
            db.StoreMovements.Add(new StoreMovement
            {
                StoreItemId    = item.Id,
                ItemCode       = item.ItemCode,
                ItemName       = item.Name,
                MovementType   = StoreMovementType.Receipt,
                QuantityBefore = 0,
                QuantityChange = req.QuantityInStock,
                QuantityAfter  = req.QuantityInStock,
                Reference      = "Initial stock",
                Notes          = "Opening balance on item creation",
                MovedByEmail   = UserEmail,
                MovedByName    = UserName,
                CreatedAt      = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
        log.LogInformation("✅ StoreItem created: {Code} by {User}", code, UserEmail);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, ToDto(item));
    }

    // ── PUT /api/v1/store/items/{id} ──────────────────────────────────────────
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,DepartmentManager,Supervisor,StoreOfficer")]
    public async Task<ActionResult<StoreItemDto>> Update(Guid id, [FromBody] UpdateStoreItemRequest req)
    {
        var item = await db.StoreItems.FindAsync(id);
        if (item is null) return NotFound();

        item.Name          = req.Name.Trim();
        item.Category      = req.Category;
        item.Unit          = req.Unit;
        item.ReorderLevel  = req.ReorderLevel;
        item.UnitCostNaira = req.UnitCostNaira;
        item.Description   = req.Description?.Trim();
        item.StoreLocation = req.StoreLocation?.Trim();
        item.Supplier      = req.Supplier?.Trim();
        item.IsActive      = req.IsActive;
        item.UpdatedAt     = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(ToDto(item));
    }

    // ── POST /api/v1/store/items/{id}/restock ────────────────────────────────
    /// <summary>Add stock to an existing item (goods received).</summary>
    [HttpPost("{id:guid}/restock")]
    [Authorize(Roles = "SystemAdmin,DepartmentManager,Supervisor,StoreOfficer")]
    public async Task<ActionResult<StoreItemDto>> Restock(Guid id, [FromBody] RestockRequest req)
    {
        if (req.Quantity <= 0) return BadRequest("Quantity must be greater than zero.");

        var item = await db.StoreItems.FindAsync(id);
        if (item is null) return NotFound();
        if (!item.IsActive) return BadRequest("Item is inactive.");

        var before = item.QuantityInStock;
        item.QuantityInStock += req.Quantity;
        item.UnitCostNaira   =  req.UnitCostNaira; // update cost price
        item.UpdatedAt       =  DateTime.UtcNow;

        db.StoreMovements.Add(new StoreMovement
        {
            StoreItemId    = item.Id,
            ItemCode       = item.ItemCode,
            ItemName       = item.Name,
            MovementType   = StoreMovementType.Receipt,
            QuantityBefore = before,
            QuantityChange = req.Quantity,
            QuantityAfter  = item.QuantityInStock,
            Reference      = req.Reference?.Trim(),
            Notes          = req.Notes?.Trim(),
            MovedByEmail   = UserEmail,
            MovedByName    = UserName,
            CreatedAt      = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        log.LogInformation("📦 Restock: {Code} +{Qty} by {User}", item.ItemCode, req.Quantity, UserEmail);
        return Ok(ToDto(item));
    }

    // ── POST /api/v1/store/items/{id}/adjust ─────────────────────────────────
    /// <summary>Manual stock correction (e.g. after physical count).</summary>
    [HttpPost("{id:guid}/adjust")]
    [Authorize(Roles = "SystemAdmin,StoreOfficer")]
    public async Task<ActionResult<StoreItemDto>> Adjust(Guid id, [FromBody] AdjustStockRequest req)
    {
        if (req.NewQuantity < 0) return BadRequest("Quantity cannot be negative.");

        var item = await db.StoreItems.FindAsync(id);
        if (item is null) return NotFound();

        var before = item.QuantityInStock;
        var change = req.NewQuantity - before;
        item.QuantityInStock = req.NewQuantity;
        item.UpdatedAt       = DateTime.UtcNow;

        db.StoreMovements.Add(new StoreMovement
        {
            StoreItemId    = item.Id,
            ItemCode       = item.ItemCode,
            ItemName       = item.Name,
            MovementType   = StoreMovementType.Adjustment,
            QuantityBefore = before,
            QuantityChange = change,
            QuantityAfter  = req.NewQuantity,
            Notes          = req.Reason.Trim(),
            MovedByEmail   = UserEmail,
            MovedByName    = UserName,
            CreatedAt      = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        log.LogInformation("🔧 Stock adjustment: {Code} {Before}→{After} by {User}", item.ItemCode, before, req.NewQuantity, UserEmail);
        return Ok(ToDto(item));
    }

    // ── DELETE /api/v1/store/items/{id} ──────────────────────────────────────
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await db.StoreItems.FindAsync(id);
        if (item is null) return NotFound();

        var hasMovements = await db.StoreMovements.AnyAsync(x => x.StoreItemId == id);
        var hasReqItems  = await db.StoreRequisitionItems.AnyAsync(x => x.StoreItemId == id);

        if (hasMovements || hasReqItems)
        {
            // Soft-delete if item has history
            item.IsActive  = false;
            item.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Ok(new { message = "Item deactivated (has movement history)." });
        }

        db.StoreItems.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── GET /api/v1/store/movements ───────────────────────────────────────────
    [HttpGet("/api/v1/store/movements")]
    public async Task<ActionResult<List<StoreMovementDto>>> AllMovements(
        [FromQuery] string? itemCode = null,
        [FromQuery] string? type     = null,
        [FromQuery] int     limit    = 100)
    {
        var q = db.StoreMovements.AsQueryable();

        if (!string.IsNullOrWhiteSpace(itemCode))
            q = q.Where(x => x.ItemCode == itemCode);

        if (!string.IsNullOrWhiteSpace(type))
            q = q.Where(x => x.MovementType == type);

        var data = await q
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(m => new StoreMovementDto
            {
                Id             = m.Id,
                ItemCode       = m.ItemCode,
                ItemName       = m.ItemName,
                MovementType   = m.MovementType,
                QuantityBefore = m.QuantityBefore,
                QuantityChange = m.QuantityChange,
                QuantityAfter  = m.QuantityAfter,
                Reference      = m.Reference,
                Notes          = m.Notes,
                MovedByEmail   = m.MovedByEmail,
                MovedByName    = m.MovedByName,
                CreatedAt      = m.CreatedAt,
            })
            .ToListAsync();

        return Ok(data);
    }

    // ── Projection ────────────────────────────────────────────────────────────
    private static StoreItemDto ToDto(StoreItem x) => new()
    {
        Id              = x.Id,
        ItemCode        = x.ItemCode,
        Name            = x.Name,
        Category        = x.Category,
        Unit            = x.Unit,
        QuantityInStock = x.QuantityInStock,
        ReorderLevel    = x.ReorderLevel,
        IsLowStock      = x.QuantityInStock <= x.ReorderLevel,
        UnitCostNaira   = x.UnitCostNaira,
        TotalValueNaira = (decimal)x.QuantityInStock * x.UnitCostNaira,
        Description     = x.Description,
        StoreLocation   = x.StoreLocation,
        Supplier        = x.Supplier,
        IsActive        = x.IsActive,
        CreatedByEmail  = x.CreatedByEmail,
        CreatedAt       = x.CreatedAt,
        UpdatedAt       = x.UpdatedAt,
    };
}
