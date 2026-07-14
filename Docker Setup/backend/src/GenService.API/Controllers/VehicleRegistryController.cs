using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GenService.API.Controllers;

/// <summary>
/// Vehicle master registry + statutory document management (licence, road worthiness,
/// insurance, hackney/heavy-duty permits). Expiry monitoring and 14/7/1-day reminders
/// are driven by the background MaintenanceReminderService.
/// </summary>
[ApiController]
[Route("api/v1/vehicle-registry")]
[Authorize]
public class VehicleRegistryController(
    GenServiceDbContext db,
    ILogger<VehicleRegistryController> logger) : ControllerBase
{
    private string CallerEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";
    private string CallerName  => User.FindFirstValue(ClaimTypes.Name)  ?? "";

    private static int DaysToExpiry(DateTime expiry) =>
        (int)Math.Ceiling((expiry.Date - DateTime.UtcNow.Date).TotalDays);

    public static string DocStatusFor(DateTime expiry)
    {
        var days = DaysToExpiry(expiry);
        return days < 0   ? VehicleDocumentStatus.Expired
             : days <= 14 ? VehicleDocumentStatus.Expiring
             :              VehicleDocumentStatus.Valid;
    }

    private static VehicleDocumentDto ToDto(VehicleDocument d) => new(
        d.Id, d.VehicleId, d.VehicleRegNo, d.DocumentType, d.IssueDate, d.ExpiryDate,
        DaysToExpiry(d.ExpiryDate), d.IssuingAuthority, d.RenewalCostNaira,
        d.ReceiptAttachment, DocStatusFor(d.ExpiryDate), d.Notes,
        d.LoggedByEmail, d.LoggedByName, d.CreatedAt);

    // ═══════════════════════════════ VEHICLES ═══════════════════════════════════

    // ── GET /api/v1/vehicle-registry/vehicles ────────────────────────────────
    [HttpGet("vehicles")]
    public async Task<IActionResult> ListVehicles([FromQuery] VehicleQuery q)
    {
        var query = db.Vehicles.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Location))
            query = query.Where(v => v.AssignedLocation == q.Location);
        if (!string.IsNullOrWhiteSpace(q.Status))
            query = query.Where(v => v.OperationalStatus == q.Status);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(v =>
                v.RegistrationNumber.Contains(s) ||
                v.FleetNumber.Contains(s) ||
                (v.MakeModel != null && v.MakeModel.Contains(s)) ||
                (v.AssignedDriver != null && v.AssignedDriver.Contains(s)));
        }

        var total    = await query.CountAsync();
        var vehicles = await query
            .OrderBy(v => v.FleetNumber)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync();

        var ids  = vehicles.Select(v => v.Id).ToList();
        var docs = await db.VehicleDocuments.AsNoTracking()
            .Where(d => ids.Contains(d.VehicleId))
            .ToListAsync();

        var items = vehicles.Select(v =>
        {
            var vdocs = docs.Where(d => d.VehicleId == v.Id).ToList();
            return ToVehicleDto(v, vdocs);
        });

        return Ok(new { items, totalCount = total, page = q.Page, pageSize = q.PageSize });
    }

    private static VehicleDto ToVehicleDto(Vehicle v, List<VehicleDocument> docs) => new(
        v.Id, v.FleetNumber, v.RegistrationNumber, v.VehicleType, v.MakeModel,
        v.YearOfManufacture, v.EngineNumber, v.ChassisNumber, v.Colour,
        v.AssignedLocation, v.AssignedDriver, v.AcquisitionDate, v.OperationalStatus,
        v.Notes,
        docs.Count,
        docs.Count(d => DocStatusFor(d.ExpiryDate) == VehicleDocumentStatus.Expiring),
        docs.Count(d => DocStatusFor(d.ExpiryDate) == VehicleDocumentStatus.Expired),
        v.LoggedByEmail, v.LoggedByName, v.CreatedAt);

    // ── GET /api/v1/vehicle-registry/vehicles/{id} ───────────────────────────
    [HttpGet("vehicles/{id:guid}")]
    public async Task<IActionResult> GetVehicle(Guid id)
    {
        var v = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (v is null) return NotFound();
        var docs = await db.VehicleDocuments.AsNoTracking()
            .Where(d => d.VehicleId == id).ToListAsync();
        return Ok(new { vehicle = ToVehicleDto(v, docs), documents = docs.Select(ToDto) });
    }

    // ── POST /api/v1/vehicle-registry/vehicles ───────────────────────────────
    [HttpPost("vehicles")]
    public async Task<ActionResult<VehicleDto>> CreateVehicle([FromBody] CreateVehicleRequest req)
    {
        var reg = req.RegistrationNumber.Trim();
        if (await db.Vehicles.AnyAsync(v => v.RegistrationNumber == reg))
            return Conflict(new { message = $"A vehicle with registration '{reg}' already exists." });

        var v = new Vehicle
        {
            FleetNumber        = req.FleetNumber.Trim(),
            RegistrationNumber = reg,
            VehicleType        = req.VehicleType.Trim(),
            MakeModel          = req.MakeModel?.Trim(),
            YearOfManufacture  = req.YearOfManufacture,
            EngineNumber       = req.EngineNumber?.Trim(),
            ChassisNumber      = req.ChassisNumber?.Trim(),
            Colour             = req.Colour?.Trim(),
            AssignedLocation   = req.AssignedLocation?.Trim(),
            AssignedDriver     = req.AssignedDriver?.Trim(),
            AcquisitionDate    = req.AcquisitionDate?.Date,
            OperationalStatus  = string.IsNullOrWhiteSpace(req.OperationalStatus)
                                    ? VehicleOperationalStatus.Active : req.OperationalStatus.Trim(),
            Notes              = req.Notes?.Trim(),
            LoggedByEmail      = CallerEmail,
            LoggedByName       = CallerName,
            CreatedAt          = DateTime.UtcNow,
            UpdatedAt          = DateTime.UtcNow,
        };

        db.Vehicles.Add(v);
        await db.SaveChangesAsync();
        logger.LogInformation("Vehicle registered: {Fleet} / {Reg}", v.FleetNumber, v.RegistrationNumber);
        return CreatedAtAction(nameof(GetVehicle), new { id = v.Id }, ToVehicleDto(v, []));
    }

    // ── PUT /api/v1/vehicle-registry/vehicles/{id} ───────────────────────────
    [HttpPut("vehicles/{id:guid}")]
    public async Task<ActionResult<VehicleDto>> UpdateVehicle(Guid id, [FromBody] CreateVehicleRequest req)
    {
        var v = await db.Vehicles.FindAsync(id);
        if (v is null) return NotFound();

        v.FleetNumber        = req.FleetNumber.Trim();
        v.RegistrationNumber = req.RegistrationNumber.Trim();
        v.VehicleType        = req.VehicleType.Trim();
        v.MakeModel          = req.MakeModel?.Trim();
        v.YearOfManufacture  = req.YearOfManufacture;
        v.EngineNumber       = req.EngineNumber?.Trim();
        v.ChassisNumber      = req.ChassisNumber?.Trim();
        v.Colour             = req.Colour?.Trim();
        v.AssignedLocation   = req.AssignedLocation?.Trim();
        v.AssignedDriver     = req.AssignedDriver?.Trim();
        v.AcquisitionDate    = req.AcquisitionDate?.Date;
        if (!string.IsNullOrWhiteSpace(req.OperationalStatus))
            v.OperationalStatus = req.OperationalStatus.Trim();
        v.Notes              = req.Notes?.Trim();
        v.UpdatedAt          = DateTime.UtcNow;

        await db.SaveChangesAsync();
        var docs = await db.VehicleDocuments.AsNoTracking().Where(d => d.VehicleId == id).ToListAsync();
        return Ok(ToVehicleDto(v, docs));
    }

    // ── DELETE /api/v1/vehicle-registry/vehicles/{id} ────────────────────────
    [HttpDelete("vehicles/{id:guid}")]
    public async Task<IActionResult> DeleteVehicle(Guid id)
    {
        var v = await db.Vehicles.FindAsync(id);
        if (v is null) return NotFound();
        var docs = db.VehicleDocuments.Where(d => d.VehicleId == id);
        db.VehicleDocuments.RemoveRange(docs);
        db.Vehicles.Remove(v);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ═══════════════════════════════ DOCUMENTS ══════════════════════════════════

    // ── GET /api/v1/vehicle-registry/documents ───────────────────────────────
    [HttpGet("documents")]
    public async Task<IActionResult> ListDocuments([FromQuery] VehicleDocumentQuery q)
    {
        var docs = await db.VehicleDocuments.AsNoTracking().ToListAsync();
        var filtered = docs.AsEnumerable();

        if (q.VehicleId.HasValue)
            filtered = filtered.Where(d => d.VehicleId == q.VehicleId.Value);
        if (!string.IsNullOrWhiteSpace(q.DocumentType))
            filtered = filtered.Where(d => d.DocumentType == q.DocumentType);
        if (!string.IsNullOrWhiteSpace(q.Status))
            filtered = filtered.Where(d => DocStatusFor(d.ExpiryDate) == q.Status);

        var ordered = filtered.OrderBy(d => d.ExpiryDate).ToList();
        var total   = ordered.Count;
        var paged   = ordered.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize);

        return Ok(new { items = paged.Select(ToDto), totalCount = total, page = q.Page, pageSize = q.PageSize });
    }

    // ── GET /api/v1/vehicle-registry/documents/expiring ──────────────────────
    /// <summary>Documents expiring within the next {days} days (default 30) or already expired.</summary>
    [HttpGet("documents/expiring")]
    public async Task<IActionResult> ExpiringDocuments([FromQuery] int days = 30)
    {
        var docs = await db.VehicleDocuments.AsNoTracking().ToListAsync();
        var upcoming = docs
            .Where(d => DaysToExpiry(d.ExpiryDate) <= days)
            .OrderBy(d => d.ExpiryDate)
            .Select(ToDto)
            .ToList();
        return Ok(upcoming);
    }

    // ── POST /api/v1/vehicle-registry/documents ──────────────────────────────
    [HttpPost("documents")]
    public async Task<ActionResult<VehicleDocumentDto>> CreateDocument([FromBody] CreateVehicleDocumentRequest req)
    {
        var regNo = req.VehicleRegNo.Trim();
        if (string.IsNullOrWhiteSpace(regNo))
            return BadRequest(new { message = "Vehicle registration is required." });

        // Link to a registered vehicle if the registration matches; otherwise stand alone.
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.RegistrationNumber == regNo);

        var doc = new VehicleDocument
        {
            VehicleId         = vehicle?.Id ?? Guid.Empty,
            VehicleRegNo      = regNo,
            DocumentType      = req.DocumentType.Trim(),
            IssueDate         = req.IssueDate?.Date ?? DateTime.UtcNow.Date,
            ExpiryDate        = req.ExpiryDate.Date,
            IssuingAuthority  = req.IssuingAuthority?.Trim(),
            RenewalCostNaira  = req.RenewalCostNaira,
            ReceiptAttachment = req.ReceiptAttachment?.Trim(),
            Status            = DocStatusFor(req.ExpiryDate),
            Notes             = req.Notes?.Trim(),
            LoggedByEmail     = CallerEmail,
            LoggedByName      = CallerName,
            CreatedAt         = DateTime.UtcNow,
            UpdatedAt         = DateTime.UtcNow,
        };

        db.VehicleDocuments.Add(doc);
        await db.SaveChangesAsync();
        logger.LogInformation("Vehicle document added: {Type} for {Reg} expiring {Expiry:d}",
            doc.DocumentType, doc.VehicleRegNo, doc.ExpiryDate);
        return CreatedAtAction(nameof(ListDocuments), ToDto(doc));
    }

    // ── POST /api/v1/vehicle-registry/documents/{id}/renew ───────────────────
    [HttpPost("documents/{id:guid}/renew")]
    public async Task<ActionResult<VehicleDocumentDto>> RenewDocument(Guid id, [FromBody] RenewVehicleDocumentRequest req)
    {
        var doc = await db.VehicleDocuments.FindAsync(id);
        if (doc is null) return NotFound();

        doc.IssueDate         = req.IssueDate?.Date ?? DateTime.UtcNow.Date;
        doc.ExpiryDate        = req.ExpiryDate.Date;
        if (req.RenewalCostNaira  is not null) doc.RenewalCostNaira  = req.RenewalCostNaira;
        if (req.IssuingAuthority  is not null) doc.IssuingAuthority  = req.IssuingAuthority.Trim();
        if (req.ReceiptAttachment is not null) doc.ReceiptAttachment = req.ReceiptAttachment.Trim();
        if (req.Notes             is not null) doc.Notes             = req.Notes.Trim();
        doc.Status              = DocStatusFor(doc.ExpiryDate);
        doc.LastReminderDaysOut = null;
        doc.ExpiredNotified     = false;
        doc.UpdatedAt           = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(ToDto(doc));
    }

    // ── DELETE /api/v1/vehicle-registry/documents/{id} ───────────────────────
    [HttpDelete("documents/{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id)
    {
        var d = await db.VehicleDocuments.FindAsync(id);
        if (d is null) return NotFound();
        db.VehicleDocuments.Remove(d);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ═══════════════════════════════ SUMMARY ════════════════════════════════════

    // ── GET /api/v1/vehicle-registry/summary ─────────────────────────────────
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var vehicles = await db.Vehicles.AsNoTracking().ToListAsync();
        var docs     = await db.VehicleDocuments.AsNoTracking().ToListAsync();

        return Ok(new
        {
            totalVehicles    = vehicles.Count,
            activeVehicles   = vehicles.Count(v => v.OperationalStatus == VehicleOperationalStatus.Active),
            groundedVehicles = vehicles.Count(v => v.OperationalStatus == VehicleOperationalStatus.Grounded),
            totalDocuments   = docs.Count,
            validDocuments   = docs.Count(d => DocStatusFor(d.ExpiryDate) == VehicleDocumentStatus.Valid),
            expiringDocuments= docs.Count(d => DocStatusFor(d.ExpiryDate) == VehicleDocumentStatus.Expiring),
            expiredDocuments = docs.Count(d => DocStatusFor(d.ExpiryDate) == VehicleDocumentStatus.Expired),
        });
    }
}
