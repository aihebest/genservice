import { apiClient } from './client';

/**
 * Link to the Logistics platform (logistics.desiconapp.com).
 *
 * The Logistics team owns the fleet and sends vehicles here for repair, so their
 * register is the authority on which vehicles exist. These endpoints let the
 * maintenance form offer real vehicles, and let a manager fix up any request
 * whose plate didn't match.
 *
 * Note: apiClient already has `/api/v1` in its baseURL — paths here must not
 * repeat it.
 */

export interface LogisticsVehicle {
  id:             string;
  registrationNo: string;
  make?:          string | null;
  model?:         string | null;
  year?:          number | null;
  assetTagNo?:    string | null;
  status?:        string | null;
  odometerKm?:    number | null;
}

export interface UnmatchedVehicleRequest {
  requestId:     string;
  requestNumber: string;
  vehicleRegNo:  string;
  vehicleType:   string;
  status:        string;
  syncStatus?:   string | null;
  syncError?:    string | null;
  createdAt:     string;
}

export interface IntegrationHealth {
  logisticsConfigured: boolean;
  logisticsReachable:  boolean;
  logisticsBaseUrl?:   string | null;
  linkedRequests:      number;
  unmatchedRequests:   number;
  failedSyncs:         number;
  lastError?:          string | null;
}

const BASE = '/integration';

export const integrationApi = {
  /** The Logistics fleet, for the vehicle picker. Empty if Logistics is unreachable. */
  fleet: () =>
    apiClient.get<LogisticsVehicle[]>(`${BASE}/fleet`).then(r => r.data),

  /** Requests whose registration matched no Logistics vehicle. */
  unmatched: () =>
    apiClient.get<UnmatchedVehicleRequest[]>(`${BASE}/unmatched`).then(r => r.data),

  /** Bind an unmatched request to a real Logistics vehicle (manager only). */
  resolve: (id: string, logisticsVehicleId: string, logisticsRegistrationNo: string) =>
    apiClient
      .post<UnmatchedVehicleRequest>(`${BASE}/unmatched/${id}/resolve`, {
        logisticsVehicleId,
        logisticsRegistrationNo,
      })
      .then(r => r.data),

  /** Re-send a request that previously failed to reach Logistics (manager only). */
  resync: (id: string) =>
    apiClient.post<UnmatchedVehicleRequest>(`${BASE}/requests/${id}/resync`).then(r => r.data),

  health: () =>
    apiClient.get<IntegrationHealth>(`${BASE}/health`).then(r => r.data),
};

export default integrationApi;
