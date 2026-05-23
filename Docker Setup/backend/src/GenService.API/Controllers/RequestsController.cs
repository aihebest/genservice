using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GenService.API.Controllers;

[ApiController]
[Route("api/v1/requests")]
[Authorize]
public class RequestsController(
    GenServiceDbContext db,
    ILogger<RequestsController> logger) : ControllerBase
{
    // ── Helpers ───────────────────────────────────────────────────────────────
    private string CallerEmail   => User.FindFirstValue(ClaimTypes.Email)  ?? "";
    private string CallerName    => User.FindFirstValue(ClaimTypes.Name)   ?? "";
    private string CallerRole    => User.FindFirstValue(ClaimTypes.Role)   ?? "";

    private static RequestDto ToDto(ServiceRequest r) => new(
        r.Id, r.TicketNumber, r.Title, r.Description,
        r.Category, r.RequiresApproval, r.Status, r.Priority, r.Location,
        r.RequestedByEmail, r.RequestedByName,
        r.AssignedToEmail, r.AssignedToName,
        r.ApprovedByEmail, r.ApprovedByName,
        r.CreatedAt, r.UpdatedAt, r.ApprovedAt, r.CompletedAt,
        r.RejectionReason, r.Notes
    );

    private async Task<string> NextTicketNumberAsync()
    {
        var year  = DateTime.UtcNow.Year;
        var count = await db.ServiceRequests
                            .CountAsync(r => r.CreatedAt.Year == year);
        return $"REQ-{year}-{(count + 1):D4}";
    }

    // ── GET /api/v1/requests ─────────────────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<RequestListResponse>> List([FromQuery] RequestsQuery q)
    {
        var query = db.ServiceRequests.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Status))
            query = query.Where(r => r.Status == q.Status);

        if (!string.IsNullOrWhiteSpace(q.Category))
            query = query.Where(r => r.Category == q.Category);

        if (!string.IsNullOrWhiteSpace(q.Priority))
            query = query.Where(r => r.Priority == q.Priority);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.ToLower();
            query = query.Where(r =>
                r.Title.ToLower().Contains(s)          ||
                r.TicketNumber.ToLower().Contains(s)   ||
                r.RequestedByName.ToLower().Contains(s)||
                r.Location.ToLower().Contains(s));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(r => ToDto(r))
            .ToListAsync();

        return Ok(new RequestListResponse(items, total, q.Page, q.PageSize));
    }

    // ── GET /api/v1/requests/stats ───────────────────────────────────────────
    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var all = await db.ServiceRequests.ToListAsync();
        return Ok(new
        {
            total           = all.Count,
            open            = all.Count(r => r.Status == RequestStatus.Open),
            pendingApproval = all.Count(r => r.Status == RequestStatus.PendingApproval),
            approved        = all.Count(r => r.Status == RequestStatus.Approved),
            inProgress      = all.Count(r => r.Status == RequestStatus.InProgress),
            completed       = all.Count(r => r.Status == RequestStatus.Completed),
            rejected        = all.Count(r => r.Status == RequestStatus.Rejected),
        });
    }

    // ── GET /api/v1/requests/{id} ────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RequestDto>> GetById(Guid id)
    {
        var r = await db.ServiceRequests.FindAsync(id);
        if (r is null) return NotFound(new { message = $"Request {id} not found." });
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/requests ────────────────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<RequestDto>> Create([FromBody] CreateRequestRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { message = "Title is required." });

        var requiresApproval = RequestCategory.NeedsApproval(req.Category);
        var initialStatus    = requiresApproval
            ? RequestStatus.PendingApproval
            : RequestStatus.Open;

        var ticket = new ServiceRequest
        {
            Id               = Guid.NewGuid(),
            TicketNumber     = await NextTicketNumberAsync(),
            Title            = req.Title.Trim(),
            Description      = req.Description?.Trim() ?? "",
            Category         = req.Category,
            RequiresApproval = requiresApproval,
            Status           = initialStatus,
            Priority         = req.Priority,
            Location         = req.Location?.Trim() ?? "",
            RequestedByEmail = CallerEmail,
            RequestedByName  = CallerName,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow,
        };

        db.ServiceRequests.Add(ticket);
        await db.SaveChangesAsync();

        logger.LogInformation("Request {Ticket} created by {User} (category: {Cat}, approval: {Approx})",
            ticket.TicketNumber, CallerEmail, ticket.Category, requiresApproval);

        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ToDto(ticket));
    }

    // ── PATCH /api/v1/requests/{id}/status ──────────────────────────────────
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<RequestDto>> UpdateStatus(
        Guid id, [FromBody] UpdateStatusRequest req)
    {
        var r = await db.ServiceRequests.FindAsync(id);
        if (r is null) return NotFound(new { message = $"Request {id} not found." });

        r.Status    = req.Status;
        r.UpdatedAt = DateTime.UtcNow;

        if (req.Status == RequestStatus.InProgress && r.AssignedToEmail is null)
            r.AssignedToEmail = CallerEmail;

        if (req.Status == RequestStatus.Completed)
            r.CompletedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(req.Notes))
            r.Notes = req.Notes;

        await db.SaveChangesAsync();
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/requests/{id}/approve ──────────────────────────────────
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<RequestDto>> Approve(
        Guid id, [FromBody] ApproveRequestRequest req)
    {
        if (CallerRole is not ("DepartmentManager" or "Supervisor" or "SystemAdmin"))
            return Forbid();

        var r = await db.ServiceRequests.FindAsync(id);
        if (r is null) return NotFound(new { message = $"Request {id} not found." });

        if (r.Status != RequestStatus.PendingApproval)
            return BadRequest(new { message = "Only requests in PendingApproval status can be approved." });

        r.Status         = RequestStatus.Approved;
        r.ApprovedByEmail= CallerEmail;
        r.ApprovedByName = CallerName;
        r.ApprovedAt     = DateTime.UtcNow;
        r.UpdatedAt      = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(req.Notes))
            r.Notes = req.Notes;

        await db.SaveChangesAsync();

        logger.LogInformation("Request {Ticket} approved by {User}", r.TicketNumber, CallerEmail);
        return Ok(ToDto(r));
    }

    // ── POST /api/v1/requests/{id}/reject ────────────────────────────────────
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<RequestDto>> Reject(
        Guid id, [FromBody] RejectRequestRequest req)
    {
        if (CallerRole is not ("DepartmentManager" or "Supervisor" or "SystemAdmin"))
            return Forbid();

        var r = await db.ServiceRequests.FindAsync(id);
        if (r is null) return NotFound(new { message = $"Request {id} not found." });

        if (r.Status != RequestStatus.PendingApproval)
            return BadRequest(new { message = "Only requests in PendingApproval status can be rejected." });

        r.Status          = RequestStatus.Rejected;
        r.ApprovedByEmail = CallerEmail;
        r.ApprovedByName  = CallerName;
        r.ApprovedAt      = DateTime.UtcNow;
        r.RejectionReason = req.Reason?.Trim();
        r.UpdatedAt       = DateTime.UtcNow;

        await db.SaveChangesAsync();

        logger.LogInformation("Request {Ticket} rejected by {User}: {Reason}",
            r.TicketNumber, CallerEmail, req.Reason);

        return Ok(ToDto(r));
    }

    // ── POST /api/v1/requests/{id}/assign ────────────────────────────────────
    [HttpPost("{id:guid}/assign")]
    public async Task<ActionResult<RequestDto>> Assign(
        Guid id, [FromBody] AssignRequestRequest req)
    {
        if (CallerRole is not ("DepartmentManager" or "Supervisor" or "SystemAdmin"))
            return Forbid();

        var r = await db.ServiceRequests.FindAsync(id);
        if (r is null) return NotFound(new { message = $"Request {id} not found." });

        r.AssignedToEmail = req.AssigneeEmail;
        r.AssignedToName  = req.AssigneeName;
        r.Status          = RequestStatus.InProgress;
        r.UpdatedAt       = DateTime.UtcNow;

        await db.SaveChangesAsync();

        logger.LogInformation("Request {Ticket} assigned to {Assignee} by {By}",
            r.TicketNumber, req.AssigneeEmail, CallerEmail);

        return Ok(ToDto(r));
    }

    // ── DELETE /api/v1/requests/{id} (cancel) ───────────────────────────────
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<RequestDto>> Cancel(Guid id)
    {
        var r = await db.ServiceRequests.FindAsync(id);
        if (r is null) return NotFound(new { message = $"Request {id} not found." });

        if (r.Status is RequestStatus.Completed or RequestStatus.Cancelled)
            return BadRequest(new { message = "Cannot cancel a completed or already-cancelled request." });

        r.Status    = RequestStatus.Cancelled;
        r.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(ToDto(r));
    }
}
