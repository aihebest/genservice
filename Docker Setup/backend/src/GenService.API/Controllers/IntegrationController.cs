using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;
using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using GenService.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenService.API.Controllers;

/// <summary>
/// Machine-to-machine endpoints shared with the Logistics Platform.
///
/// The inbound half (/logistics/*) is called by the Logistics API with a shared
/// secret — no user session — so it is NOT [Authorize]d; it is gated by
/// <see cref="RequireIntegrationKeyAttribute"/> instead.
///
/// The operator half (fleet picker, reconciliation queue) IS called by signed-in
/// General Service staff and carries the normal [Authorize].
/// </summary>
[ApiController]
[Route("api/v1/integration")]
public class IntegrationController(
    GenServiceDbContext   db,
    LogisticsSyncService  logistics,
    NotificationService   notify,
    ILogger<IntegrationController> logger) : ControllerBase
{
    private string CallerRole => User.FindFirstValue(ClaimTypes.Role) ?? "";
    private bool   IsManagerOrAbove =>
        CallerRole is "DepartmentManager" or "SystemAdmin" or "Supervisor";

    // ══════════════════════════════════════════════════════════════════════════
    //  INBOUND — called by the Logistics Platform
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The Logistics team has reported a fault on one of their vehicles. Raise it
    /// here as a Pending Vehicle Maintenance Request and hand back our reference.
    ///
    /// Idempotent: re-posting the same LogisticsRecordId returns the existing
    /// request rather than creating a duplicate, so their retries are safe.
    /// </summary>
    [HttpPost("logistics/vehicle-requests")]
    [RequireIntegrationKey]
    public async Task<ActionResult<LogisticsVehicleRequestAck>> ReceiveFromLogistics(
        [FromBody] LogisticsVehicleRequestPayload p)
    {
        if (string.IsNullOrWhiteSpace(p.VehicleRegNo))
            return BadRequest(new { message = "vehicleRegNo is required." });

        var existing = await db.VehicleMaintenanceRequests
            .FirstOrDefaultAsync(r => r.LogisticsRecordId == p.LogisticsRecordId);

        if (existing is not null)
        {
            logger.LogInformation("Logistics record {Rec} already linked to {Num} — returning existing.",
                p.LogisticsRecordId, existing.RequestNumber);
            return Ok(new LogisticsVehicleRequestAck(
                existing.Id, existing.RequestNumber, existing.Status,
                "Already raised — existing request returned."));
        }

        // Every string is capped to the column width. Logistics sizes its columns
        // independently of ours, and an over-long plate or vehicle type would
        // otherwise fail the insert with a truncation error — exactly the failure
        // that broke the new-request form before.
        var reg = Cap(p.VehicleRegNo.Trim().ToUpperInvariant(), 60);

        var r = new VehicleMaintenanceRequest
        {
            RequestNumber      = await NextRequestNumberAsync(),
            VehicleRegNo       = reg,
            VehicleType        = Cap((p.VehicleType ?? "").Trim(), 200),
            AssetNo            = Cap(p.AssetNo?.Trim(), 50),
            MaintenanceType    = IntegrationStatusMap.ToGenServiceMaintenanceType(p.Category, p.ServiceType),
            Description        = Cap(BuildDescription(p), 2000),
            Priority           = NormalisePriority(p.Priority),
            Status             = VehicleMaintenanceStatus.Pending,
            CurrentLocation    = Cap((p.CurrentLocation ?? "").Trim(), 200),
            OdometerReading    = p.OdometerKm?.ToString(CultureInfo.InvariantCulture),
            NotificationStatus = "Open",
            DateOfRequest      = ParseDay(p.DateReported) ?? DateTime.UtcNow.Date,
            WorkshopName       = Cap(p.VendorName?.Trim(), 200),
            Notes              = p.Notes?.Trim(),

            // Requester is the Logistics person, not a GenService user.
            RequestedByEmail   = Cap((p.RequestedByEmail ?? "logistics@desicongroup.com").Trim(), 150),
            RequestedByName    = Cap(string.IsNullOrWhiteSpace(p.RequestedByName)
                                    ? "Logistics Platform"
                                    : p.RequestedByName.Trim(), 100),

            // Cross-reference — this is what makes the feedback loop work.
            LogisticsRecordId  = p.LogisticsRecordId,
            LogisticsVehicleId = p.LogisticsVehicleId == Guid.Empty ? null : p.LogisticsVehicleId,
            SourceSystem       = RequestSourceSystem.Logistics,
            LogisticsSyncStatus = LogisticsSyncState.Synced,
            LogisticsSyncedAt   = DateTime.UtcNow,

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.VehicleMaintenanceRequests.Add(r);
        await db.SaveChangesAsync();

        logger.LogInformation("Logistics raised {Num} for {Reg} (record {Rec}).",
            r.RequestNumber, r.VehicleRegNo, p.LogisticsRecordId);

        // Same alert GS management gets for a request raised on this platform —
        // flagged so they know it came from Logistics and a vehicle is waiting.
        await notify.CreateAsync(
            title:      "🚚 Vehicle sent in by Logistics — awaiting approval",
            // Message is nvarchar(500) but Description allows 2000 — cap it, or a
            // wordy fault report fails the notification insert and, with it, the
            // whole hand-off from Logistics.
            message:    Cap($"{r.RequestNumber} — {r.VehicleRegNo} ({r.VehicleType}) reported by {r.RequestedByName}. {r.Description}", 500),
            type:       NotificationType.MaintenancePending,
            module:     "VehicleMaintenance",
            entityId:   r.Id.ToString(),
            refNumber:  r.RequestNumber,
            targetRole: NotificationTarget.Management);

        return Ok(new LogisticsVehicleRequestAck(
            r.Id, r.RequestNumber, r.Status, "Vehicle maintenance request raised."));
    }

    /// <summary>
    /// Lightweight status read for the Logistics platform — lets them confirm the
    /// current state of a request without waiting for our next push.
    /// </summary>
    [HttpGet("logistics/vehicle-requests/{logisticsRecordId:guid}")]
    [RequireIntegrationKey]
    public async Task<ActionResult<GenServiceStatusPushPayload>> ReadStatusForLogistics(Guid logisticsRecordId)
    {
        var r = await db.VehicleMaintenanceRequests
            .FirstOrDefaultAsync(x => x.LogisticsRecordId == logisticsRecordId);

        if (r is null)
            return NotFound(new { message = "No General Service request is linked to that record." });

        return Ok(new GenServiceStatusPushPayload(
            GenServiceRequestId:     r.Id,
            GenServiceRequestNumber: r.RequestNumber,
            LogisticsRecordId:       r.LogisticsRecordId,
            LogisticsVehicleId:      r.LogisticsVehicleId,
            VehicleRegNo:            r.VehicleRegNo,
            GenServiceStatus:        r.Status,
            Status:                  IntegrationStatusMap.ToLogistics(r.Status),
            VehicleOutOfService:     IntegrationStatusMap.IsOutOfService(r.Status),
            MaintenanceType:         r.MaintenanceType,
            Description:             r.Description,
            WorkshopName:            r.WorkshopName,
            WorkshopLocation:        r.WorkshopLocation,
            FaultIdentified:         r.FaultIdentified,
            ProposedSolution:        r.ProposedSolution,
            WorkDone:                r.WorkDone,
            ActionedBy:              r.ActionedBy,
            RejectionReason:         r.RejectionReason,
            Cost:                    r.FinalAmountNaira ?? r.AmountNaira ?? r.SparesCostNaira,
            DateReported:            (r.DateOfRequest ?? r.CreatedAt).ToString("yyyy-MM-dd"),
            CompletedDate:           r.CompletedAt?.ToString("yyyy-MM-dd"),
            DateReturned:            r.DateHandedOver?.ToString("yyyy-MM-dd"),
            Notes:                   r.Notes,
            UpdatedAt:               r.UpdatedAt));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  OPERATOR — called by signed-in General Service staff
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The Logistics fleet, for the vehicle picker on the new-request form.
    /// Returns an empty list if Logistics is unreachable — the form falls back to
    /// free text rather than blocking the user.
    /// </summary>
    [HttpGet("fleet")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<LogisticsVehicleDto>>> Fleet()
        => Ok(await logistics.GetFleetAsync(HttpContext.RequestAborted));

    /// <summary>
    /// Requests whose registration number matched no vehicle in the Logistics
    /// registry. These are not lost — they work normally here, they just aren't
    /// feeding back to Logistics until someone binds them to the right vehicle.
    /// </summary>
    [HttpGet("unmatched")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<UnmatchedVehicleRequestDto>>> Unmatched()
    {
        var rows = await db.VehicleMaintenanceRequests
            .Where(r => r.LogisticsVehicleId == null
                     && r.Status != VehicleMaintenanceStatus.Rejected)
            .OrderByDescending(r => r.CreatedAt)
            .Take(200)
            .ToListAsync();

        return Ok(rows.Select(r => new UnmatchedVehicleRequestDto(
            r.Id, r.RequestNumber, r.VehicleRegNo, r.VehicleType, r.Status,
            r.LogisticsSyncStatus, r.LogisticsSyncError, r.CreatedAt)));
    }

    /// <summary>
    /// Bind an unmatched request to a real Logistics vehicle, then push it so the
    /// Logistics team immediately sees the history they were missing.
    /// </summary>
    [HttpPost("unmatched/{id:guid}/resolve")]
    [Authorize]
    public async Task<ActionResult<UnmatchedVehicleRequestDto>> Resolve(
        Guid id, [FromBody] ResolveVehicleMatchRequest req)
    {
        if (!IsManagerOrAbove) return Forbid();

        var r = await db.VehicleMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound(new { message = $"Request {id} not found." });
        if (req.LogisticsVehicleId == Guid.Empty)
            return BadRequest(new { message = "Select a vehicle from the Logistics fleet." });

        r.LogisticsVehicleId = req.LogisticsVehicleId;
        if (!string.IsNullOrWhiteSpace(req.LogisticsRegistrationNo))
            r.VehicleRegNo = req.LogisticsRegistrationNo.Trim().ToUpperInvariant();
        r.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await logistics.PushStatusAsync(r, HttpContext.RequestAborted);

        return Ok(new UnmatchedVehicleRequestDto(
            r.Id, r.RequestNumber, r.VehicleRegNo, r.VehicleType, r.Status,
            r.LogisticsSyncStatus, r.LogisticsSyncError, r.CreatedAt));
    }

    /// <summary>Re-send a request that previously failed to reach Logistics.</summary>
    [HttpPost("requests/{id:guid}/resync")]
    [Authorize]
    public async Task<ActionResult<UnmatchedVehicleRequestDto>> Resync(Guid id)
    {
        if (!IsManagerOrAbove) return Forbid();

        var r = await db.VehicleMaintenanceRequests.FindAsync(id);
        if (r is null) return NotFound(new { message = $"Request {id} not found." });

        // Give it one more chance to match before declaring it unlinked.
        if (r.LogisticsVehicleId is null)
        {
            var match = await logistics.MatchVehicleAsync(r.VehicleRegNo, r.AssetNo, HttpContext.RequestAborted);
            if (match is not null) r.LogisticsVehicleId = match.Id;
        }

        await logistics.PushStatusAsync(r, HttpContext.RequestAborted);

        return Ok(new UnmatchedVehicleRequestDto(
            r.Id, r.RequestNumber, r.VehicleRegNo, r.VehicleType, r.Status,
            r.LogisticsSyncStatus, r.LogisticsSyncError, r.CreatedAt));
    }

    /// <summary>Is the link to Logistics configured and working? Shown on the admin screen.</summary>
    [HttpGet("health")]
    [Authorize]
    public async Task<ActionResult<IntegrationHealthDto>> Health()
    {
        var linked    = await db.VehicleMaintenanceRequests.CountAsync(r => r.LogisticsVehicleId != null);
        var unmatched = await db.VehicleMaintenanceRequests.CountAsync(r => r.LogisticsVehicleId == null);
        var failed    = await db.VehicleMaintenanceRequests
                                .CountAsync(r => r.LogisticsSyncStatus == LogisticsSyncState.Failed);
        var lastError = await db.VehicleMaintenanceRequests
            .Where(r => r.LogisticsSyncError != null)
            .OrderByDescending(r => r.UpdatedAt)
            .Select(r => r.LogisticsSyncError)
            .FirstOrDefaultAsync();

        var reachable = logistics.IsConfigured
            && (await logistics.GetFleetAsync(HttpContext.RequestAborted)).Count > 0;

        return Ok(new IntegrationHealthDto(
            LogisticsConfigured: logistics.IsConfigured,
            LogisticsReachable:  reachable,
            LogisticsBaseUrl:    null,   // deliberately not echoed back to the browser
            LinkedRequests:      linked,
            UnmatchedRequests:   unmatched,
            FailedSyncs:         failed,
            LastError:           lastError));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> NextRequestNumberAsync()
    {
        var year  = DateTime.UtcNow.Year % 100;
        var count = await db.VehicleMaintenanceRequests
                            .CountAsync(r => r.CreatedAt.Year == DateTime.UtcNow.Year);
        return $"V/{year}/{(count + 1):D3}";
    }

    /// <summary>
    /// Logistics splits the problem across Service Type and Notes; General Service
    /// shows one Description. Join them so nothing the driver reported is lost.
    /// </summary>
    private static string BuildDescription(LogisticsVehicleRequestPayload p)
    {
        var parts = new[] { p.Description, p.ServiceType, p.Notes }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return parts.Length > 0
            ? string.Join(" — ", parts)
            : "Reported by Logistics (no description supplied).";
    }

    private static string NormalisePriority(string? p) =>
        string.IsNullOrWhiteSpace(p) ? RequestPriority.Normal : Cap(p.Trim(), 20);

    /// <summary>
    /// Trim a value to the column width. Logistics sizes its columns
    /// independently of ours, so anything arriving from them is capped rather
    /// than allowed to fail the insert on a truncation error.
    /// </summary>
    [return: NotNullIfNotNull(nameof(s))]
    private static string? Cap(string? s, int max) =>
        s is null || s.Length <= max ? s : s[..max];

    private static DateTime? ParseDay(string? s) =>
        DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                               DateTimeStyles.None, out var d)
            ? d.Date
            : null;
}
