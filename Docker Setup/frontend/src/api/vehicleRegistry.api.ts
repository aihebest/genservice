import { apiClient } from './client';
import type { VehicleRegistryRecord, VehicleDocument } from '../types';

export interface VehicleSummary {
  totalVehicles:     number;
  activeVehicles:    number;
  groundedVehicles:  number;
  totalDocuments:    number;
  validDocuments:    number;
  expiringDocuments: number;
  expiredDocuments:  number;
}

export const vehicleRegistryApi = {
  // ── Vehicles ──────────────────────────────────────────────────────────────
  // pageSize is always sent: it must match the table's page size, or the table
  // slices the server's page a second time and rows become unreachable.
  listVehicles: (params?: { location?: string; status?: string; search?: string; page?: number; pageSize?: number }) =>
    apiClient
      .get<{ items: VehicleRegistryRecord[]; totalCount: number }>('/vehicle-registry/vehicles',
        { params: { pageSize: 20, ...params } })
      .then(r => r.data),

  getVehicle: (id: string) =>
    apiClient
      .get<{ vehicle: VehicleRegistryRecord; documents: VehicleDocument[] }>(`/vehicle-registry/vehicles/${id}`)
      .then(r => r.data),

  createVehicle: (data: Record<string, unknown>) =>
    apiClient.post<VehicleRegistryRecord>('/vehicle-registry/vehicles', data).then(r => r.data),

  updateVehicle: (id: string, data: Record<string, unknown>) =>
    apiClient.put<VehicleRegistryRecord>(`/vehicle-registry/vehicles/${id}`, data).then(r => r.data),

  deleteVehicle: (id: string) => apiClient.delete(`/vehicle-registry/vehicles/${id}`),

  // ── Documents ─────────────────────────────────────────────────────────────
  // pageSize is always sent. It was omitted, so the server returned 50 per page
  // while the table showed 20 — documents 21–50 of each page were unreachable,
  // which made older documents look "replaced" when a new one was added.
  listDocuments: (params?: { vehicleId?: string; documentType?: string; status?: string; page?: number; pageSize?: number }) =>
    apiClient
      .get<{ items: VehicleDocument[]; totalCount: number }>('/vehicle-registry/documents',
        { params: { pageSize: 20, ...params } })
      .then(r => r.data),

  expiringDocuments: (days = 30) =>
    apiClient.get<VehicleDocument[]>('/vehicle-registry/documents/expiring', { params: { days } }).then(r => r.data),

  createDocument: (data: {
    vehicleRegNo:       string;
    documentType:       string;
    expiryDate:         string;
    issueDate?:         string;
    issuingAuthority?:  string;
    renewalCostNaira?:  number;
    receiptAttachment?: string;
    notes?:             string;
  }) => apiClient.post<VehicleDocument>('/vehicle-registry/documents', data).then(r => r.data),

  renewDocument: (id: string, data: {
    expiryDate:         string;
    issueDate?:         string;
    renewalCostNaira?:  number;
    issuingAuthority?:  string;
    receiptAttachment?: string;
    notes?:             string;
  }) => apiClient.post<VehicleDocument>(`/vehicle-registry/documents/${id}/renew`, data).then(r => r.data),

  deleteDocument: (id: string) => apiClient.delete(`/vehicle-registry/documents/${id}`),

  summary: () => apiClient.get<VehicleSummary>('/vehicle-registry/summary').then(r => r.data),
};
