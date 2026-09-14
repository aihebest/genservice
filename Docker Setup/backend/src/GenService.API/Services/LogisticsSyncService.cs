using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GenService.API.Services;

/// <summary>
/// Pushes vehicle-maintenance progress to the Logistics Platform.
///
/// Design rules, learned the hard way elsewhere in this codebase:
///  • A Logistics outage must never block a General Service user. Every push is
///    wrapped so a failure is recorded on the request and logged, not thrown.
///  • The sync state is persisted (LogisticsSyncStatus / LogisticsSyncError) so a
///    failed push is visible and replayable instead of silently lost.
///  • Requests with no Logistics link are skipped, not retried forever.
/// </summary>
public class LogisticsSyncService(
    GenServiceDbContext db,
    IHttpClientFactory  httpFactory,
    IConfiguration      cfg,
    ILogger<LogisticsSyncService> logger)
{
    public const string HttpClientName = "LogisticsApi";

    private string? BaseUrl => cfg["Integration:Logistics:BaseUrl"]?.TrimEnd('/');
    private string? ApiKey  => cfg["Integration:Logistics:ApiKey"];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(ApiKey);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Push the current state of a request to Logistics. Safe to call after every
    /// status change; never throws.
    /// </summary>
    public async Task PushStatusAsync(VehicleMaintenanceRequest r, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            logger.LogDebug("Logistics sync skipped for {Num} — integration not configured.", r.RequestNumber);
            return;
        }

        // Nothing to update on the other side if we were never able to match the
        // vehicle. The reconciliation screen surfaces these.
        if (r.LogisticsRecordId is null && r.LogisticsVehicleId is null)
        {
            await MarkAsync(r, LogisticsSyncState.NotLinked,
                "No matching vehicle in the Logistics fleet registry.", ct);
            return;
        }

        var payload = BuildPayload(r);

        try
        {
            var client = CreateClient();
            var res = await client.PostAsJsonAsync(
                $"{BaseUrl}/api/integration/genservice/maintenance-status", payload, Json, ct);

            if (res.IsSuccessStatusCode)
            {
                // Logistics returns the id of the record it created or updated, so a
                // request that originated here becomes linked on first successful push.
                try
                {
                    var ack = await res.Content.ReadFromJsonAsync<LogisticsAck>(Json, ct);
                    if (ack?.LogisticsRecordId is Guid id && id != Guid.Empty)
                        r.LogisticsRecordId = id;
                }
                catch { /* an empty or unexpected body is not a failure of the push itself */ }

                await MarkAsync(r, LogisticsSyncState.Synced, null, ct);
                logger.LogInformation("Pushed {Num} ({Status}) to Logistics.", r.RequestNumber, r.Status);
                return;
            }

            var body = await res.Content.ReadAsStringAsync(ct);
            await MarkAsync(r, LogisticsSyncState.Failed,
                $"HTTP {(int)res.StatusCode}: {Truncate(body, 400)}", ct);
            logger.LogWarning("Logistics rejected push for {Num}: HTTP {Code} {Body}",
                r.RequestNumber, (int)res.StatusCode, Truncate(body, 400));
        }
        catch (Exception ex)
        {
            await MarkAsync(r, LogisticsSyncState.Failed, Truncate(ex.Message, 400), ct);
            logger.LogError(ex, "Logistics push failed for {Num}.", r.RequestNumber);
        }
    }

    /// <summary>
    /// Fetch the Logistics fleet registry. Used to populate the vehicle picker and
    /// to match a typed registration number to a real vehicle.
    /// Returns an empty list rather than throwing if Logistics is unreachable.
    /// </summary>
    public async Task<IReadOnlyList<LogisticsVehicleDto>> GetFleetAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) return [];

        try
        {
            var client = CreateClient();
            var res = await client.GetAsync($"{BaseUrl}/api/integration/genservice/vehicles", ct);
            if (!res.IsSuccessStatusCode)
            {
                logger.LogWarning("Logistics fleet fetch returned HTTP {Code}.", (int)res.StatusCode);
                return [];
            }
            return await res.Content.ReadFromJsonAsync<List<LogisticsVehicleDto>>(Json, ct) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Logistics fleet fetch failed.");
            return [];
        }
    }

    /// <summary>
    /// Try to bind a request to a Logistics vehicle by registration number.
    /// Matching ignores case, spaces, hyphens and slashes, because the same plate
    /// gets typed a dozen different ways. Falls back to the asset tag.
    /// </summary>
    public async Task<LogisticsVehicleDto?> MatchVehicleAsync(
        string regNo, string? assetNo = null, CancellationToken ct = default)
        => MatchIn(await GetFleetAsync(ct), regNo, assetNo);

    /// <summary>
    /// Match against an already-fetched fleet list. Used when matching many
    /// requests at once, so the fleet is pulled from Logistics once rather than
    /// once per request.
    /// </summary>
    public static LogisticsVehicleDto? MatchIn(
        IReadOnlyList<LogisticsVehicleDto> fleet, string regNo, string? assetNo = null)
    {
        if (fleet.Count == 0) return null;

        var key = Normalise(regNo);
        var hit = fleet.FirstOrDefault(v => Normalise(v.RegistrationNo) == key);
        if (hit is not null) return hit;

        if (!string.IsNullOrWhiteSpace(assetNo))
        {
            var tag = Normalise(assetNo);
            hit = fleet.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.AssetTagNo)
                                         && Normalise(v.AssetTagNo!) == tag);
        }
        return hit;
    }

    /// <summary>Strip everything that varies between how people type the same plate.</summary>
    public static string Normalise(string s) =>
        new(s.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    // ── Internals ─────────────────────────────────────────────────────────────

    private HttpClient CreateClient()
    {
        var client = httpFactory.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Remove(RequireIntegrationKeyAttribute.HeaderName);
        client.DefaultRequestHeaders.Add(RequireIntegrationKeyAttribute.HeaderName, ApiKey);
        return client;
    }

    private static GenServiceStatusPushPayload BuildPayload(VehicleMaintenanceRequest r) => new(
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
        // The final amount is the figure that matters to Logistics; fall back to
        // the estimate, then to spares-only, so the field is rarely empty.
        Cost:                    r.FinalAmountNaira ?? r.AmountNaira ?? r.SparesCostNaira,
        DateReported:            Day(r.DateOfRequest ?? r.CreatedAt),
        CompletedDate:           Day(r.CompletedAt),
        DateReturned:            Day(r.DateHandedOver),
        Notes:                   r.Notes,
        UpdatedAt:               r.UpdatedAt
    );

    /// <summary>Date-only, formatted the way both platforms agree on. Never ISO-8601 with a time — that shifts the day across time zones.</summary>
    private static string? Day(DateTime? d) => d?.ToString("yyyy-MM-dd");

    private async Task MarkAsync(VehicleMaintenanceRequest r, string state, string? error, CancellationToken ct)
    {
        r.LogisticsSyncStatus = state;
        r.LogisticsSyncError  = error;
        if (state == LogisticsSyncState.Synced) r.LogisticsSyncedAt = DateTime.UtcNow;

        try { await db.SaveChangesAsync(ct); }
        catch (Exception ex) { logger.LogError(ex, "Could not persist sync state for {Num}.", r.RequestNumber); }
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? s : s.Length <= max ? s : s[..max];

    private sealed record LogisticsAck(Guid? LogisticsRecordId, string? Message);
}
