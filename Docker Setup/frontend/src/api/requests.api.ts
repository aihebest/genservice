import { apiClient } from './client';
import type {
  ServiceRequest,
  CreateRequestDto,
  RequestListResponse,
  RequestStats,
} from '../types';

export interface RequestQuery {
  status?:   string;
  category?: string;
  priority?: string;
  search?:   string;
  page?:     number;
  pageSize?: number;
}

export const requestsApi = {
  list: (q?: RequestQuery) =>
    apiClient.get<RequestListResponse>('/requests', { params: q }).then(r => r.data),

  stats: () =>
    apiClient.get<RequestStats>('/requests/stats').then(r => r.data),

  getById: (id: string) =>
    apiClient.get<ServiceRequest>(`/requests/${id}`).then(r => r.data),

  create: (data: CreateRequestDto) =>
    apiClient.post<ServiceRequest>('/requests', data).then(r => r.data),

  updateStatus: (id: string, status: string, notes?: string) =>
    apiClient.patch<ServiceRequest>(`/requests/${id}/status`, { status, notes }).then(r => r.data),

  lineApprove: (id: string, notes?: string) =>
    apiClient.post<ServiceRequest>(`/requests/${id}/line-approve`, { notes }).then(r => r.data),

  lineReject: (id: string, reason: string) =>
    apiClient.post<ServiceRequest>(`/requests/${id}/line-reject`, { reason }).then(r => r.data),

  approve: (id: string, notes?: string) =>
    apiClient.post<ServiceRequest>(`/requests/${id}/approve`, { notes }).then(r => r.data),

  reject: (id: string, reason: string) =>
    apiClient.post<ServiceRequest>(`/requests/${id}/reject`, { reason }).then(r => r.data),

  assign: (id: string, assigneeEmail: string, assigneeName: string) =>
    apiClient.post<ServiceRequest>(`/requests/${id}/assign`, { assigneeEmail, assigneeName }).then(r => r.data),

  reassign: (id: string, reassignToType: string, reassignToName: string, notes?: string) =>
    apiClient.post<ServiceRequest>(`/requests/${id}/reassign`, { reassignToType, reassignToName, notes }).then(r => r.data),

  cancel: (id: string) =>
    apiClient.delete<ServiceRequest>(`/requests/${id}`).then(r => r.data),
};
