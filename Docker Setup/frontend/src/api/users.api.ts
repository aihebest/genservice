import { apiClient } from './client';
import type {
  AppUserRecord,
  UserListResponse,
  UserSummary,
  CreateUserPayload,
  UpdateUserPayload,
  ResetPasswordResponse,
} from '../types';

const BASE = '/api/v1/users';

export interface UserListQuery {
  role?:      string;
  department?:string;
  isActive?:  boolean;
  search?:    string;
  page?:      number;
  pageSize?:  number;
}

const usersApi = {
  list: async (params: UserListQuery = {}): Promise<UserListResponse> => {
    const q = new URLSearchParams();
    if (params.role)       q.set('role',       params.role);
    if (params.department) q.set('department', params.department);
    if (params.isActive !== undefined) q.set('isActive', String(params.isActive));
    if (params.search)     q.set('search',     params.search);
    if (params.page)       q.set('page',       String(params.page));
    if (params.pageSize)   q.set('pageSize',   String(params.pageSize));
    const res = await apiClient.get<UserListResponse>(`${BASE}?${q}`);
    return res.data;
  },

  summary: async (): Promise<UserSummary> => {
    const res = await apiClient.get<UserSummary>(`${BASE}/summary`);
    return res.data;
  },

  getById: async (id: string): Promise<AppUserRecord> => {
    const res = await apiClient.get<AppUserRecord>(`${BASE}/${id}`);
    return res.data;
  },

  create: async (payload: CreateUserPayload): Promise<AppUserRecord> => {
    const res = await apiClient.post<AppUserRecord>(BASE, payload);
    return res.data;
  },

  update: async (id: string, payload: UpdateUserPayload): Promise<AppUserRecord> => {
    const res = await apiClient.put<AppUserRecord>(`${BASE}/${id}`, payload);
    return res.data;
  },

  deactivate: async (id: string): Promise<AppUserRecord> => {
    const res = await apiClient.post<AppUserRecord>(`${BASE}/${id}/deactivate`);
    return res.data;
  },

  activate: async (id: string): Promise<AppUserRecord> => {
    const res = await apiClient.post<AppUserRecord>(`${BASE}/${id}/activate`);
    return res.data;
  },

  resetPassword: async (id: string): Promise<ResetPasswordResponse> => {
    const res = await apiClient.post<ResetPasswordResponse>(`${BASE}/${id}/reset-password`);
    return res.data;
  },

  changeMyPassword: async (currentPassword: string, newPassword: string): Promise<void> => {
    await apiClient.post(`${BASE}/me/change-password`, { currentPassword, newPassword });
  },

  delete: async (id: string): Promise<void> => {
    await apiClient.delete(`${BASE}/${id}`);
  },
};

export default usersApi