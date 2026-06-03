import { apiClient } from './client';
import type { VehicleMaintenance, VehicleMaintenanceListResponse, VehicleMaintenanceStats } from '../types';

export interface VMQuery {
  status?:   string;
  type?:     string;
  search?:   string;
  page?:     number;
  pageSize?: number;
}

export const vehicleMaintenanceApi = {
  list: (q?: VMQuery) =>
    apiClient.get<VehicleMaintenanceListResponse>('/vehicle-maintenance', { params: q }).then(r => r.data),

  stats: () =>
    apiClient.get<VehicleMaintenanceStats>('/vehicle-maintenance/stats').then(r => r.data),

  getById: (id: string) =>
    apiClient.get<VehicleMaintenance>(`/vehicle-maintenance/${id}`).then(r => r.data),

  create: (data: {
    vehicleRegNo:    string;
    vehicleType:     string;
    maintenanceType: string;
    description:     string;
    priority:        string;
    currentLocation: string;
  }) => apiClient.post<VehicleMaintenance>('/vehicle-maintenance', data).then(r => r.data),

  approve: (id: string, notes?: string) =>
    apiClient.post<VehicleMaintenance>(`/vehicle-maintenance/${id}/approve`, { notes }).then(r => r.data),

  reject: (id: string, reason: string) =>
    apiClient.post<VehicleMaintenance>(`/vehicle-maintenance/${id}/reject`, { reason }).then(r => r.data),

  dispatch: (id: string, workshopName: string, workshopLocation?: string) =>
    apiClient.post<VehicleMaintenance>(`/vehicle-maintenance/${id}/dispatch`, { workshopName, workshopLocation }).then(r => r.data),

  complete: (id: string, notes?: string) =>
    apiClient.post<VehicleMaintenance>(`/vehicle-maintenance/${id}/complete`, { notes }).then(r => r.data),
};
