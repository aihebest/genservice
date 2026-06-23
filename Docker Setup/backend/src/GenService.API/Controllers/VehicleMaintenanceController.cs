using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/vehicle-maintenance")]
[Authorize]
public class VehicleMaintenanceController(
    GenServiceDbContext db,
    ILogger<VehicleMaintenanceController> logger) : ControllerBase
{
    private string CallerEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";
    private string CallerName  => User.FindFirstValue(ClaimTypes.Name)  ?? "";
    private string CallerRole  => User.FindFirstValue(ClaimTypes.Role)  ?? "";

    private static VehicleMaintenanceDto ToDto(VehicleMaintenanceRequest r)
    {
        var now        = DateTime.UtcNow;
        var daysOpen   = (int)(now - r.CreatedAt).TotalDays;
        var daysInShop = r.SentToWorkshopAt.HasValue
            ? (int?)(int)(now - r.SentToWorkshopAt.Value).TotalDays
            : null;

        return new VehicleMaintenanceDto(
            r.Id, r.RequestNumber,
            r.VehicleRegNo, r.VehicleType, r.MaintenanceType,
            r.Description, r.Priority, r.Status, r.CurrentLocation, r.OdometerReading,
            r.RequestedByEmail, r.RequestedByName,
            r.ApprovedByEmail, r.ApprovedByName, r.ApprovedAt, r.RejectionReason,
            // workshop
            r.WorkshopName, r.WorkshopLocation, r.DateDeliveredToWorkshop, r.SentToWorkshopAt,
            // assessment
            r.FaultIdentified, r.ProposedSolution, r.ResolutionType,
            // parts
            r.PartsRequired, r.PartsSource, r.ProcurementMethod, r.PartsSuppliedBy, r.SparesCostNaira,
            // completion
            r.WorkDone, r.ActionedBy, r.CompletedAt,
            // handover
            r.HandoverConfirmed, r.DateHandedOver, r.HandedOverBy,
            r.Notes, r.CreatedAt, r.UpdatedAt,
            daysOpen, daysInShop
        );
    }

    private async Task<string> NextRequestNumberAsync()
    {
        var year  = DateTime.UtcNow.Year % 100;
        var count = await db.VehicleMaintenanceRequests
                            .CountAsync(r => r.CreatedAt.Year == DateTime.UtcNow.Year);
        return $"V/{year}/{(count + 1):D3}";
    }

    // ── GET /api/v1/vehicle-maintenance ──────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<VehicleMaintenanceListResponse>> List(
        [FromQuery] VehicleMaintenanceQuery q)
    {
        var query = db.VehicleMaintenanceRequests.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Status))
            query = query.Where(r => r.Status == q.Status);

        if (!string.IsNullOrWhiteSpace(q.Type))
            query = query.Where(r => r.MaintenanceType == q.Type);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.ToLower();
            query = query.Where(r =>
                r.RequestNumber.ToLower().Contains(s) ||
                r.VehicleRegNo.ToLower().Contains(s)  ||
                r.VehicleType.ToLower().Contains(s)   ||
                r.Description.ToLower().Contains(s)   ||
                r.RequestedByName.ToLower().Contains(s));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        return Ok(new VehicleMaintenanceListResponse(
            items.Select(ToDto), total, q.Page, q.PageSize));
    }

    // ── GET /api/v1/vehicle-maintenance/stats ────────────────────────────────
    [HttpGet("stats")]
    public async Task<ActionResult<VehicleMaintenanceStatsDto>> Stats()
    {
        var now        = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var all        = await db.VehicleMaintenanceRequests.ToListAsync();

        return Ok(new VehicleMaintenanceStatsDto(
            Pending:            all.Count(r => r.Status == VehicleMaintenanceStatus.Pending),
            Approved:           all.Count(r => r.Status == VehicleMaintenanceStatus.Approved),
            InWorkshop:         all.Count(r => r.Status == VehicleMaintenanceStatus.InWorkshop),
            AwaitingParts:      all.Count(r => r.Status == VehicleMaintenanceStatus.AwaitingParts),
            AwaitingFunds:      all.Count(r => r.Status == VehicleMaintenanceStatus.AwaitingFunds),
            CompletedThisMonth: all.Count(r => r.Status == VehicleMaintenanceStatus.Completed
                                            && r.CompletedAt >= monthStart),
            Rejected:           all.Count(r => r.Status == VehicleMaintenanceStatus.Rejected),
            LongStanding:       all.Count(r => r.Status == VehicleMaintenanceStatus.InWorkshop
                                            && r.SentToWorkshopAt.HasValue
                                            && (now - r.SentToWorkshopAt.Value).TotalDays > 7)
        ));
    }

    // ── GET /api/v1/vehicle-maintenance/{id} ─────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VehicleMaintenanceDto>> GetById(Guid id)
    {
        var r = await db.VehicleMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound(new { message = $"Request {id} not found." });
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/vehicle-maintenance ─────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<VehicleMaintenanceDto>> Create(
        [FromBody] CreateVehicleMaintenanceRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.VehicleRegNo))
            return BadRequest(new { message = "Vehicle registration number is required." });

        var r = new VehicleMaintenanceRequest
        {
            RequestNumber    = await NextRequestNumberAsync(),
            VehicleRegNo     = req.VehicleRegNo.Trim().ToUpper(),
            VehicleType      = req.VehicleType.Trim(),
            MaintenanceType  = req.MaintenanceType,
            Description      = req.Description.Trim(),
            Priority         = req.Priority,
            CurrentLocation  = req.CurrentLocation.Trim(),
            OdometerReading  = req.OdometerReading?.Trim(),
            RequestedByEmail = CallerEmail,
            RequestedByName  = CallerName,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow,
        };

        db.VehicleMaintenanceRequests.Add(r);
        await db.SaveChangesAsync();

        logger.LogInformation("Vehicle maintenance {Num} created by {User} for {Reg}",
            r.RequestNumber, CallerEmail, r.VehicleRegNo);

        return CreatedAtAction(nameof(GetById), new { id = r.Id }, ToDto(r));
    }

    // ── POST /api/v1/vehicle-maintenance/{id}/approve ────────────────────────
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<VehicleMaintenanceDto>> Approve(
        Guid id, [FromBody] ApproveVehicleMaintenanceRequest req)
    {
        if (CallerRole is not ("DepartmentManager" or "Supervisor" or "SystemAdmin"))
            return Forbid();

        var r = await db.VehicleMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound(new { message = $"Request {id} not found." });
        if (r.Status != VehicleMaintenanceStatus.Pending)
            return BadRequest(new { message = "Only Pending requests can be approved." });

        r.Status          = VehicleMaintenanceStatus.Approved;
        r.ApprovedByEmail = CallerEmail;
        r.ApprovedByName  = CallerName;
        r.ApprovedAt      = DateTime.UtcNow;
        r.Notes           = req.Notes ?? r.Notes;
        r.UpdatedAt       = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/vehicle-maintenance/{id}/reject ─────────────────────────
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<VehicleMaintenanceDto>> Reject(
        Guid id, [FromBody] RejectVehicleMaintenanceRequest req)
    {
        if (CallerRole is not ("DepartmentManager" or "Supervisor" or "SystemAdmin"))
            return Forbid();

        var r = await db.VehicleMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound(new { message = $"Request {id} not found." });
        if (r.Status != VehicleMaintenanceStatus.Pending)
            return BadRequest(new { message = "Only Pending requests can be rejected." });

        r.Status          = VehicleMaintenanceStatus.Rejected;
        r.ApprovedByEmail = CallerEmail;
        r.ApprovedByName  = CallerName;
        r.ApprovedAt      = DateTime.UtcNow;
        r.RejectionReason = req.Reason?.Trim();
        r.UpdatedAt       = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/vehicle-maintenance/{id}/dispatch ───────────────────────
    [HttpPost("{id:guid}/dispatch")]
    public async Task<ActionResult<VehicleMaintenanceDto>> Dispatch(
        Guid id, [FromBody] SendToWorkshopRequest req)
    {
        if (CallerRole is not ("DepartmentManager" or "Supervisor" or "SystemAdmin"))
            return Forbid();

        var r = await db.VehicleMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound(new { message = $"Request {id} not found." });
        if (r.Status != VehicleMaintenanceStatus.Approved)
            return BadRequest(new { message = "Only Approved requests can be dispatched to workshop." });

        r.Status                   = VehicleMaintenanceStatus.InWorkshop;
        r.WorkshopName             = req.WorkshopName.Trim();
        r.WorkshopLocation         = req.WorkshopLocation?.Trim();
        r.DateDeliveredToWorkshop  = req.DateDeliveredToWorkshop ?? DateTime.UtcNow;
        r.SentToWorkshopAt         = DateTime.UtcNow;
        r.UpdatedAt                = DateTime.UtcNow;

        await db.SaveChangesAsync();
        logger.LogInformation("{Num} dispatched to {Workshop} by {User}",
            r.RequestNumber, req.WorkshopName, CallerEmail);
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/vehicle-maintenance/{id}/assess ─────────────────────────
    /// <summary>Record workshop fault assessment and parts requirements</summary>
    [HttpPost("{id:guid}/assess")]
    public async Task<ActionResult<VehicleMaintenanceDto>> Assess(
        Guid id, [FromBody] VehicleAssessmentRequest req)
    {
        var r = await db.VehicleMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound(new { message = $"Request {id} not found." });
        if (r.Status is VehicleMaintenanceStatus.Completed or VehicleMaintenanceStatus.Rejected)
            return BadRequest(new { message = "Cannot assess a Completed or Rejected request." });

        r.FaultIdentified  = req.FaultIdentified?.Trim();
        r.ProposedSolution = req.ProposedSolution?.Trim();
        r.ResolutionType   = req.ResolutionType;
        r.PartsRequired    = req.PartsRequired;
        r.PartsSource      = req.PartsSource;
        r.ProcurementMethod= req.ProcurementMethod;
        r.PartsSuppliedBy  = req.PartsSuppliedBy?.Trim();
        r.SparesCostNaira  = req.SparesCostNaira;

        // auto-advance status if waiting for parts/funds
        if (req.PartsRequired && req.PartsSource == "NewPurchase" &&
            r.Status == VehicleMaintenanceStatus.InWorkshop)
        {
            r.Status = req.ProcurementMethod == "CashAdvance"
                ? VehicleMaintenanceStatus.AwaitingFunds
                : VehicleMaintenanceStatus.AwaitingParts;
        }

        r.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("{Num} assessed by {User}: {Fault}",
            r.RequestNumber, CallerEmail, req.FaultIdentified);
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/vehicle-maintenance/{id}/complete ───────────────────────
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<VehicleMaintenanceDto>> Complete(
        Guid id, [FromBody] CompleteVehicleMaintenanceRequest req)
    {
        if (CallerRole is not ("DepartmentManager" or "Supervisor" or "SystemAdmin"))
            return Forbid();

        var r = await db.VehicleMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound(new { message = $"Request {id} not found." });

        var completableStatuses = new[]
        {
            VehicleMaintenanceStatus.InWorkshop,
            VehicleMaintenanceStatus.AwaitingParts,
            VehicleMaintenanceStatus.AwaitingFunds,
        };
        if (!completableStatuses.Contains(r.Status))
            return BadRequest(new { message = "Request must be InWorkshop, AwaitingParts, or AwaitingFunds to complete." });

        r.Status          = VehicleMaintenanceStatus.Completed;
        r.WorkDone        = req.WorkDone?.Trim();
        r.ActionedBy      = req.ActionedBy?.Trim() ?? CallerName;
        r.SparesCostNaira = req.SparesCostNaira ?? r.SparesCostNaira;
        r.Notes           = req.Notes ?? r.Notes;
        r.CompletedAt     = DateTime.UtcNow;
        r.UpdatedAt       = DateTime.UtcNow;

        await db.SaveChangesAsync();
        logger.LogInformation("{Num} completed by {User}", r.RequestNumber, CallerEmail);
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/vehicle-maintenance/{id}/handover ───────────────────────
    /// <summary>Confirm vehicle has been returned to the requestor/user</summary>
    [HttpPost("{id:guid}/handover")]
    public async Task<ActionResult<VehicleMaintenanceDto>> Handover(
        Guid id, [FromBody] VehicleHandoverRequest req)
    {
        var r = await db.VehicleMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound(new { message = $"Request {id} not found." });
        if (r.Status != VehicleMaintenanceStatus.Completed)
            return BadRequest(new { message = "Handover can only be confirmed after completion." });

        r.HandoverConfirmed = true;
        r.HandedOverBy      = req.HandedOverBy.Trim();
        r.DateHandedOver    = req.DateHandedOver ?? DateTime.UtcNow;
        r.UpdatedAt         = DateTime.UtcNow;

        await db.SaveChangesAsync();
        logger.LogInformation("{Num} handed over by {Staff} on {Date}",
            r.RequestNumber, req.HandedOverBy, r.DateHandedOver);
        return Ok(ToDto(r));
    }
}
