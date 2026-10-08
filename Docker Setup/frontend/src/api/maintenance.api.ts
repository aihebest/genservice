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

  update: (id: string, patch: Partial<CreateScheduleRequest> & { isActive?: boolean }) =>
    apiClient.patch<MaintenanceSchedule>(`/maintenance/${id}`, patch)
             .then(r => r.data),

  /** `completedOn` (YYYY-MM-DD) is when the work actually happened; defaults to today on the server. */
  complete: (id: string, completedByEmail: string, completedByName: string, notes?: string,
             completedOn?: string, doneBy?: string) =>
    apiClient.post<MaintenanceSchedule>(`/maintenance/${id}/complete`, {
      notes, completedByEmail, completedByName, completedOn, doneBy,
    }).then(r => r.data),

  /** Completion history — every time a scheduled task was actually carried out. */
  completions: (q: { scheduleId?: string; category?: string; from?: string; to?: string } = {}) =>
    apiClient.get<ScheduleCompletion[]>('/maintenance/completions', { params: q }).then(r => r.data),

  upcoming: (days = 30) =>
    apiClient.get<MaintenanceSchedule[]>('/maintenance/upcoming', { params: { days } })
             .then(r => r.data),

  escalations: () =>
    apiClient.get<MaintenanceSchedule[]>('/maintenance/escalations')
             .then(r => r.data),

  snoozeReminder: (id: string, hoursToSnooze: number) =>
    apiClient.post<MaintenanceSchedule>(`/maintenance/${id}/snooze-reminder`, { hoursToSnooze })
             .then(r => r.data),

  delete: (id: string) =>
    apiClient.delete(`/maintenance/${id}`),
};

/** One entry in the scheduler's completion log. */
export interface ScheduleCompletion {
  id:           string;
  scheduleId:   string;
  taskName:     string;
  category:     string;
  location?:    string;
  completedOn:  string;   // YYYY-MM-DD
  doneBy?:      string;
  notes?:       string;
  loggedByName: string;
  createdAt:    string;
}
