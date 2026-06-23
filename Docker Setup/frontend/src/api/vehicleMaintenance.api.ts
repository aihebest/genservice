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
    odometerReading?: string;
  }) => apiClient.post<VehicleMaintenance>('/vehicle-maintenance', data).then(r => r.data),

  approve: (id: string, notes?: string) =>
    apiClient.post<VehicleMaintenance>(`/vehicle-maintenance/${id}/approve`, { notes }).then(r => r.data),

  reject: (id: string, reason: string) =>
    apiClient.post<VehicleMaintenance>(`/vehicle-maintenance/${id}/reject`, { reason }).then(r => r.data),

  dispatch: (id: string, data: {
    workshopName:            string;
    workshopLocation?:       string;
    dateDeliveredToWorkshop?: string;
  }) => apiClient.post<VehicleMaintenance>(`/vehicle-maintenance/${id}/dispatch`, data).then(r => r.data),

  assess: (id: string, data: {
    faultIdentified?:   string;
    proposedSolution?:  string;
    resolutionType?:    string;
    partsRequired:      boolean;
    partsSource?:       string;
    procurementMethod?: string;
    partsSuppliedBy?:   string;
    sparesCostNaira?:   number;
  }) => apiClient.post<VehicleMaintenance>(`/vehicle-maintenance/${id}/assess`, data).then(r => r.data),

  complete: (id: string, data: {
    workDone?:       string;
    actionedBy?:     string;
    sparesCostNaira?: number;
    notes?:          string;
  }) => apiClient.post<VehicleMaintenance>(`/vehicle-maintenance/${id}/complete`, data).then(r => r.data),

  handover: (id: string, data: {
    handedOverBy:   string;
    dateHandedOver?: string;
  }) => apiClient.post<VehicleMaintenance>(`/vehicle-maintenance/${id}/handover`, data).then(r => r.data),
};
