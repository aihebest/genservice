import { apiClient } from './client';
import type {
  FacilityMaintenance,
  FacilityMaintenanceListResponse,
  FacilityMaintenanceStats,
} from '../types';

export interface FacilityQuery {
  status?:   string;
  type?:     string;
  search?:   string;
  page?:     number;
  pageSize?: number;
}

export const facilityMaintenanceApi = {
  list: (q?: FacilityQuery) =>
    apiClient.get<FacilityMaintenanceListResponse>('/facility-maintenance', { params: q }).then(r => r.data),

  stats: () =>
    apiClient.get<FacilityMaintenanceStats>('/facility-maintenance/stats').then(r => r.data),

  getById: (id: string) =>
    apiClient.get<FacilityMaintenance>(`/facility-maintenance/${id}`).then(r => r.data),

  create: (data: {
    maintenanceType: string;
    description:     string;
    location:        string;
    endUser:         string;
    roomFlat?:       string;
    priority:        string;
  }) => apiClient.post<FacilityMaintenance>('/facility-maintenance', data).then(r => r.data),

  approve: (id: string, notes?: string) =>
    apiClient.post<FacilityMaintenance>(`/facility-maintenance/${id}/approve`, { notes }).then(r => r.data),

  reject: (id: string, reason: string) =>
    apiClient.post<FacilityMaintenance>(`/facility-maintenance/${id}/reject`, { reason }).then(r => r.data),

  updateStatus: (id: string, status: string) =>
    apiClient.patch<FacilityMaintenance>(`/facility-maintenance/${id}/status`, { status }).then(r => r.data),

  complete: (id: string, workDone: string, actionedBy?: string, notes?: string) =>
    apiClient.post<FacilityMaintenance>(`/facility-maintenance/${id}/complete`, { workDone, actionedBy, notes }).then(r => r.data),
};
