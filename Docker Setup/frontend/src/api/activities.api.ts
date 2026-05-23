import { apiClient } from './client';
import type {
  StaffActivity,
  ActivityListResponse,
  LogActivityRequest,
} from '../types';

interface ListParams {
  status?:    string;
  category?:  string;
  staffEmail?: string;
  search?:    string;
  page?:      number;
  pageSize?:  number;
}

export const activitiesApi = {
  list: (params?: ListParams) =>
    apiClient.get<ActivityListResponse>('/activities', { params })
             .then(r => r.data),

  getActive: () =>
    apiClient.get<StaffActivity[]>('/activities/active')
             .then(r => r.data),

  getById: (id: string) =>
    apiClient.get<StaffActivity>(`/activities/${id}`)
             .then(r => r.data),

  log: (req: LogActivityRequest) =>
    apiClient.post<StaffActivity>('/activities', req)
             .then(r => r.data),

  proxyLog: (req: LogActivityRequest) =>
    apiClient.post<StaffActivity>('/activities/proxy', req)
             .then(r => r.data),

  updateStatus: (id: string, status: string, notes?: string) =>
    apiClient.patch<StaffActivity>(`/activities/${id}/status`, { status, notes })
             .then(r => r.data),

  delete: (id: string) =>
    apiClient.delete(`/activities/${id}`),
};
