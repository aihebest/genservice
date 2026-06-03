using GenService.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/audit")]
[Authorize]
public class AuditController(GenServiceDbContext db) : ControllerBase
{
    // ── GET /api/v1/audit?entityType=Request&entityId=xxx ────────────────────
    /// <summary>Full audit trail for a specific entity.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? entityType = null,
        [FromQuery] string? entityId   = null,
        [FromQuery] string? refNumber  = null,
        [FromQuery] int     days       = 90,
        [FromQuery] int     page       = 1,
        [FromQuery] int     pageSize   = 50)
    {
        var from  = DateTime.UtcNow.AddDays(-days);
        var query = db.AuditEntries.AsNoTracking()
                      .Where(a => a.Timestamp >= from);

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(entityId))
            query = query.Where(a => a.EntityId == entityId);
        if (!string.IsNullOrWhiteSpace(refNumber))
            query = query.Where(a => a.RefNumber == refNumber);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id, a.EntityType, a.EntityId, a.RefNumber,
                a.Action, a.OldValue, a.NewValue, a.Details,
                a.PerformedByEmail, a.PerformedByName, a.Timestamp,
            })
            .ToListAsync();

        return Ok(new { items, totalCount = total, page, pageSize });
    }
}
