import { apiClient } from './client';
import type {
  EquipmentMaintenance,
  EquipmentMaintenanceListResponse,
  EquipmentMaintenanceStats,
} from '../types';

export interface EquipmentQuery {
  status?:   string;
  type?:     string;
  search?:   string;
  page?:     number;
  pageSize?: number;
}

export const equipmentMaintenanceApi = {
  list: (q?: EquipmentQuery) =>
    apiClient.get<EquipmentMaintenanceListResponse>('/equipment-maintenance', { params: q }).then(r => r.data),

  stats: () =>
    apiClient.get<EquipmentMaintenanceStats>('/equipment-maintenance/stats').then(r => r.data),

  getById: (id: string) =>
    apiClient.get<EquipmentMaintenance>(`/equipment-maintenance/${id}`).then(r => r.data),

  create: (data: {
    assetNo:          string;
    assetDescription: string;
    maintenanceType:  string;
    endUser:          string;
    location:         string;
    description:      string;
    priority:         string;
    runningHours?:    number;
    nextServiceHour?: number;
  }) => apiClient.post<EquipmentMaintenance>('/equipment-maintenance', data).then(r => r.data),

  approve: (id: string, notes?: string) =>
    apiClient.post<EquipmentMaintenance>(`/equipment-maintenance/${id}/approve`, { notes }).then(r => r.data),

  reject: (id: string, reason: string) =>
    apiClient.post<EquipmentMaintenance>(`/equipment-maintenance/${id}/reject`, { reason }).then(r => r.data),

  updateStatus: (id: string, status: string) =>
    apiClient.patch<EquipmentMaintenance>(`/equipment-maintenance/${id}/status`, { status }).then(r => r.data),

  assess: (id: string, data: {
    faultIdentified?:   string;
    proposedSolution?:  string;
    resolutionType?:    string;
    partsRequired:      boolean;
    partsSource?:       string;
    procurementMethod?: string;
    sparesCostNaira?:   number;
  }) => apiClient.post<EquipmentMaintenance>(`/equipment-maintenance/${id}/assess`, data).then(r => r.data),

  complete: (id: string, data: {
    workDone?:        string;
    actionedBy?:      string;
    sparesCostNaira?: number;
    notes?:           string;
  }) => apiClient.post<EquipmentMaintenance>(`/equipment-maintenance/${id}/complete`, data).then(r => r.data),

  handover: (id: string, data: {
    handedOverBy:    string;
    dateHandedOver?: string;
  }) => apiClient.post<EquipmentMaintenance>(`/equipment-maintenance/${id}/handover`, data).then(r => r.data),
};
