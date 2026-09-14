using GenService.API.Domain;

namespace GenService.API.Models;

// ═══════════════════════════════════════════════════════════════════════════════
//  GenService ↔ Logistics Platform integration contract
//
//  The Logistics team owns the fleet. When one of their vehicles develops a
//  fault they raise it in their own platform; it arrives here as a Vehicle
//  Maintenance Request. Every status change we make is pushed straight back to
//  them so they always know where their vehicle is.
//
//  Both directions authenticate with a shared secret in the X-Integration-Key
//  header. Neither system ever holds a user session for the other.
// ═══════════════════════════════════════════════════════════════════════════════

// ── Inbound: Logistics raises a maintenance request here ──────────────────────

public record LogisticsVehicleRequestPayload(
    /// <summary>Id of the MaintenanceRecord on the Logistics side — our cross-reference.</summary>
    Guid     LogisticsRecordId,
    /// <summary>Id of the vehicle in the Logistics fleet registry.</summary>
    Guid     LogisticsVehicleId,
    string   VehicleRegNo,
    string?  VehicleType       = null,
    string?  AssetNo           = null,
    /// <summary>"Routine" or "FaultRepair" in Logistics terms.</summary>
    string?  Category          = null,
    /// <summary>Logistics service type — Oil Change, Brakes, Engine, etc.</summary>
    string?  ServiceType       = null,
    string?  Description       = null,
    string?  Priority          = null,
    string?  CurrentLocation   = null,
    int?     OdometerKm        = null,
    /// <summary>YYYY-MM-DD — the Logistics "Date Reported".</summary>
    string?  DateReported      = null,
    string?  VendorName        = null,
    string?  RequestedByEmail  = null,
    string?  RequestedByName   = null,
    string?  Notes             = null
);

public record LogisticsVehicleRequestAck(
    Guid   RequestId,
    string RequestNumber,
    string Status,
    string Message
);

// ── Outbound: what we push to Logistics on every status change ────────────────

public record GenServiceStatusPushPayload(
    Guid      GenServiceRequestId,
    string    GenServiceRequestNumber,
    Guid?     LogisticsRecordId,
    Guid?     LogisticsVehicleId,
    string    VehicleRegNo,
    /// <summary>Our own status — kept verbatim so Logistics can show the real reason.</summary>
    string    GenServiceStatus,
    /// <summary>Already mapped into the Logistics vocabulary.</summary>
    string    Status,
    /// <summary>True while the vehicle is off the road (in workshop / awaiting parts or funds).</summary>
    bool      VehicleOutOfService,
    string?   MaintenanceType   = null,
    string?   Description       = null,
    string?   WorkshopName      = null,
    string?   WorkshopLocation  = null,
    string?   FaultIdentified   = null,
    string?   ProposedSolution  = null,
    string?   WorkDone          = null,
    string?   ActionedBy        = null,
    string?   RejectionReason   = null,
    decimal?  Cost              = null,
    /// <summary>YYYY-MM-DD</summary>
    string?   DateReported      = null,
    string?   CompletedDate     = null,
    string?   DateReturned      = null,
    string?   Notes             = null,
    DateTime? UpdatedAt         = null
);

// ── Logistics fleet registry, mirrored here for the vehicle picker ────────────

public record LogisticsVehicleDto(
    Guid    Id,
    string  RegistrationNo,
    string? Make,
    string? Model,
    int?    Year,
    string? AssetTagNo,
    string? Status,
    int?    OdometerKm
);

/// <summary>A GenService request whose registration number matched no Logistics vehicle.</summary>
public record UnmatchedVehicleRequestDto(
    Guid     RequestId,
    string   RequestNumber,
    string   VehicleRegNo,
    string   VehicleType,
    string   Status,
    string?  SyncStatus,
    string?  SyncError,
    DateTime CreatedAt
);

public record ResolveVehicleMatchRequest(
    Guid   LogisticsVehicleId,
    string LogisticsRegistrationNo
);

public record IntegrationHealthDto(
    bool     LogisticsConfigured,
    bool     LogisticsReachable,
    string?  LogisticsBaseUrl,
    int      LinkedRequests,
    int      UnmatchedRequests,
    int      FailedSyncs,
    string?  LastError
);

// ── Status vocabulary mapping ─────────────────────────────────────────────────

/// <summary>
/// The two platforms use different status words for the same thing. This is the
/// single place that translation lives — keep it here rather than scattering
/// string comparisons through the controllers.
/// </summary>
public static class IntegrationStatusMap
{
    /// <summary>GenService status → the Logistics MaintenanceRecord status.</summary>
    public static string ToLogistics(string genServiceStatus) => genServiceStatus switch
    {
        VehicleMaintenanceStatus.Pending       => "Scheduled",
        VehicleMaintenanceStatus.Approved      => "Scheduled",
        VehicleMaintenanceStatus.InWorkshop    => "InProgress",
        VehicleMaintenanceStatus.AwaitingParts => "InProgress",
        VehicleMaintenanceStatus.AwaitingFunds => "InProgress",
        VehicleMaintenanceStatus.Completed     => "Completed",
        VehicleMaintenanceStatus.Rejected      => "Cancelled",
        _                                      => "Scheduled",
    };

    /// <summary>Is the vehicle physically off the road at this status?</summary>
    public static bool IsOutOfService(string genServiceStatus) =>
        genServiceStatus is VehicleMaintenanceStatus.InWorkshop
                         or VehicleMaintenanceStatus.AwaitingParts
                         or VehicleMaintenanceStatus.AwaitingFunds;

    /// <summary>Logistics category + service type → our MaintenanceType.</summary>
    public static string ToGenServiceMaintenanceType(string? category, string? serviceType)
    {
        if (string.Equals(category, "FaultRepair", StringComparison.OrdinalIgnoreCase))
        {
            // Engine and gearbox work is major by any reasonable reading; the GS
            // manager can still change it on the request.
            var major = serviceType is not null &&
                        (serviceType.Contains("Engine",   StringComparison.OrdinalIgnoreCase) ||
                         serviceType.Contains("Gearbox",  StringComparison.OrdinalIgnoreCase) ||
                         serviceType.Contains("Overhaul", StringComparison.OrdinalIgnoreCase));
            return major ? VehicleMaintenanceType.MajorRepair : VehicleMaintenanceType.MinorRepair;
        }
        return VehicleMaintenanceType.RoutineService;
    }
}
