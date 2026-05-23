import { apiClient } from './client';
import type {
  MaintenanceSchedule,
  ScheduleListResponse,
  MaintenanceStats,
  CreateScheduleRequest,
} from '../types';

interface ListParams {
  category?:    string;
  overdueOnly?: boolean;
  activeOnly?:  boolean;
  search?:      string;
  page?:        number;
  pageSize?:    number;
}

export const maintenanceApi = {
  list: (params?: ListParams) =>
    apiClient.get<ScheduleListResponse>('/maintenance', { params })
             .then(r => r.data),

  stats: () =>
    apiClient.get<MaintenanceStats>('/maintenance/stats')
             .then(r => r.data),

  getById: (id: string) =>
    apiClient.get<MaintenanceSchedule>(`/maintenance/${id}`)
             .then(r => r.data),

  create: (req: CreateScheduleRequest) =>
    apiClient.post<MaintenanceSchedule>('/maintenance', req)
             .then(r => r.data),

  complete: (id: string, completedByEmail: string, completedByName: string, notes?: string) =>
    apiClient.post<MaintenanceSchedule>(`/maintenance/${id}/complete`, {
      notes, completedByEmail, completedByName,
    }).then(r => r.data),

  delete: (id: string) =>
    apiClient.delete(`/maintenance/${id}`),
};
