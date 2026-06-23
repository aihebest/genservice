using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/facility-maintenance")]
[Authorize]
public class FacilityMaintenanceController(
    GenServiceDbContext db,
    ILogger<FacilityMaintenanceController> logger) : ControllerBase
{
    private string CallerEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";
    private string CallerName  => User.FindFirstValue(ClaimTypes.Name)  ?? "";
    private string CallerRole  => User.FindFirstValue(ClaimTypes.Role)  ?? "";

    private static FacilityMaintenanceDto ToDto(FacilityMaintenanceRequest r) => new(
        r.Id, r.RequestNumber,
        r.MaintenanceType, r.Description,
        r.Location, r.EndUser, r.RoomFlat,
        r.Priority, r.Status,
        r.RequestedByEmail, r.RequestedByName,
        r.ApprovedByEmail, r.ApprovedByName, r.ApprovedAt, r.RejectionReason,
        // assessment
        r.FaultIdentified, r.ProposedSolution, r.ResolutionType,
        // parts
        r.PartsRequired, r.PartsSource, r.ProcurementMethod, r.SparesCostNaira,
        // completion
        r.WorkDone, r.ActionedBy, r.CompletedAt,
        // handover
        r.HandoverConfirmed, r.DateHandedOver, r.HandedOverBy,
        r.Notes, r.CreatedAt, r.UpdatedAt,
        (int)(DateTime.UtcNow - r.CreatedAt).TotalDays
    );

    private async Task<string> NextRefAsync()
    {
        var yr    = DateTime.UtcNow.Year % 100;
        var count = await db.FacilityMaintenanceRequests
                            .CountAsync(r => r.CreatedAt.Year == DateTime.UtcNow.Year);
        return $"F/{yr}/{(count + 1):D3}";
    }

    // ── GET /api/v1/facility-maintenance ─────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<FacilityMaintenanceListResponse>> List(
        [FromQuery] FacilityQuery q)
    {
        var query = db.FacilityMaintenanceRequests.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Status))
            query = query.Where(r => r.Status == q.Status);
        if (!string.IsNullOrWhiteSpace(q.Type))
            query = query.Where(r => r.MaintenanceType == q.Type);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.ToLower();
            query = query.Where(r =>
                r.RequestNumber.ToLower().Contains(s)   ||
                r.Description.ToLower().Contains(s)     ||
                r.Location.ToLower().Contains(s)        ||
                r.EndUser.ToLower().Contains(s)         ||
                r.RequestedByName.ToLower().Contains(s) ||
                (r.RoomFlat != null && r.RoomFlat.ToLower().Contains(s)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return Ok(new FacilityMaintenanceListResponse(
            items.Select(ToDto), total, q.Page, q.PageSize));
    }

    // ── GET /api/v1/facility-maintenance/stats ───────────────────────────────
    [HttpGet("stats")]
    public async Task<ActionResult<FacilityMaintenanceStatsDto>> Stats()
    {
        var now        = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var all        = await db.FacilityMaintenanceRequests.ToListAsync();

        return Ok(new FacilityMaintenanceStatsDto(
            all.Count(r => r.Status == MaintenanceRequestStatus.Pending),
            all.Count(r => r.Status == MaintenanceRequestStatus.Approved),
            all.Count(r => r.Status == MaintenanceRequestStatus.Ongoing),
            all.Count(r => r.Status == MaintenanceRequestStatus.AwaitingSpares),
            all.Count(r => r.Status == MaintenanceRequestStatus.AwaitingFunds),
            all.Count(r => r.Status == MaintenanceRequestStatus.Completed && r.CompletedAt >= monthStart),
            all.Count(r => r.Status == MaintenanceRequestStatus.Rejected)
        ));
    }

    // ── GET /api/v1/facility-maintenance/{id} ────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FacilityMaintenanceDto>> GetById(Guid id)
    {
        var r = await db.FacilityMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound();
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/facility-maintenance ────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<FacilityMaintenanceDto>> Create(
        [FromBody] CreateFacilityMaintenanceRequest req)
    {
        var r = new FacilityMaintenanceRequest
        {
            RequestNumber    = await NextRefAsync(),
            MaintenanceType  = req.MaintenanceType,
            Description      = req.Description.Trim(),
            Location         = req.Location.Trim(),
            EndUser          = req.EndUser.Trim(),
            RoomFlat         = req.RoomFlat?.Trim(),
            Priority         = req.Priority,
            RequestedByEmail = CallerEmail,
            RequestedByName  = CallerName,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow,
        };
        db.FacilityMaintenanceRequests.Add(r);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = r.Id }, ToDto(r));
    }

    // ── POST /api/v1/facility-maintenance/{id}/approve ───────────────────────
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<FacilityMaintenanceDto>> Approve(
        Guid id, [FromBody] ApproveFacilityRequest req)
    {
        if (CallerRole is not ("DepartmentManager" or "Supervisor" or "SystemAdmin")) return Forbid();
        var r = await db.FacilityMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound();
        if (r.Status != MaintenanceRequestStatus.Pending)
            return BadRequest(new { message = "Only Pending requests can be approved." });

        r.Status          = MaintenanceRequestStatus.Approved;
        r.ApprovedByEmail = CallerEmail;
        r.ApprovedByName  = CallerName;
        r.ApprovedAt      = DateTime.UtcNow;
        r.Notes           = req.Notes ?? r.Notes;
        r.UpdatedAt       = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/facility-maintenance/{id}/reject ────────────────────────
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<FacilityMaintenanceDto>> Reject(
        Guid id, [FromBody] RejectFacilityRequest req)
    {
        if (CallerRole is not ("DepartmentManager" or "Supervisor" or "SystemAdmin")) return Forbid();
        var r = await db.FacilityMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound();
        if (r.Status != MaintenanceRequestStatus.Pending)
            return BadRequest(new { message = "Only Pending requests can be rejected." });

        r.Status          = MaintenanceRequestStatus.Rejected;
        r.ApprovedByEmail = CallerEmail;
        r.ApprovedByName  = CallerName;
        r.ApprovedAt      = DateTime.UtcNow;
        r.RejectionReason = req.Reason?.Trim();
        r.UpdatedAt       = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/facility-maintenance/{id}/assess ────────────────────────
    [HttpPost("{id:guid}/assess")]
    public async Task<ActionResult<FacilityMaintenanceDto>> Assess(
        Guid id, [FromBody] FacilityAssessmentRequest req)
    {
        var r = await db.FacilityMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound();

        r.FaultIdentified  = req.FaultIdentified?.Trim();
        r.ProposedSolution = req.ProposedSolution?.Trim();
        r.ResolutionType   = req.ResolutionType;
        r.PartsRequired    = req.PartsRequired;
        r.PartsSource      = req.PartsSource;
        r.ProcurementMethod= req.ProcurementMethod;
        r.SparesCostNaira  = req.SparesCostNaira;

        if (req.PartsRequired && req.PartsSource == "NewPurchase" &&
            r.Status == MaintenanceRequestStatus.Ongoing)
        {
            r.Status = req.ProcurementMethod == "CashAdvance"
                ? MaintenanceRequestStatus.AwaitingFunds
                : MaintenanceRequestStatus.AwaitingSpares;
        }

        r.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ToDto(r));
    }

    // ── PATCH /api/v1/facility-maintenance/{id}/status ───────────────────────
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<FacilityMaintenanceDto>> UpdateStatus(
        Guid id, [FromBody] UpdateStatusRequest req)
    {
        var r = await db.FacilityMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound();

        r.Status    = req.Status;
        r.UpdatedAt = DateTime.UtcNow;
        if (req.Status == MaintenanceRequestStatus.Completed)
            r.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/facility-maintenance/{id}/complete ──────────────────────
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<FacilityMaintenanceDto>> Complete(
        Guid id, [FromBody] CompleteFacilityRequest req)
    {
        if (CallerRole is not ("DepartmentManager" or "Supervisor" or "SystemAdmin" or "Technician"))
            return Forbid();
        var r = await db.FacilityMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound();

        r.Status          = MaintenanceRequestStatus.Completed;
        r.WorkDone        = req.WorkDone?.Trim();
        r.ActionedBy      = req.ActionedBy?.Trim() ?? CallerName;
        r.SparesCostNaira = req.SparesCostNaira ?? r.SparesCostNaira;
        r.Notes           = req.Notes ?? r.Notes;
        r.CompletedAt     = DateTime.UtcNow;
        r.UpdatedAt       = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/facility-maintenance/{id}/handover ──────────────────────
    [HttpPost("{id:guid}/handover")]
    public async Task<ActionResult<FacilityMaintenanceDto>> Handover(
        Guid id, [FromBody] FacilityHandoverRequest req)
    {
        var r = await db.FacilityMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound();
        if (r.Status != MaintenanceRequestStatus.Completed)
            return BadRequest(new { message = "Handover can only be confirmed after completion." });

        r.HandoverConfirmed = true;
        r.HandedOverBy      = req.HandedOverBy.Trim();
        r.DateHandedOver    = req.DateHandedOver ?? DateTime.UtcNow;
        r.UpdatedAt         = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ToDto(r));
    }
}
