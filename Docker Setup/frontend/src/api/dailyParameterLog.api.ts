import { apiClient } from './client';
import type {
  DailyParameterLog,
  DailyParameterLogListResponse,
  DailyParameterLogStats,
  CreateDailyParameterLogPayload,
  UpdateDailyParameterLogPayload,
} from '../types';

const BASE = '/api/v1/daily-parameter-log';

export interface DailyLogQuery {
  location?: string;
  from?:     string;   // "YYYY-MM-DD"
  to?:       string;
  page?:     number;
  pageSize?: number;
}

const dailyParameterLogApi = {
  list: async (params: DailyLogQuery = {}): Promise<DailyParameterLogListResponse> => {
    const q = new URLSearchParams();
    if (params.location) q.set('location', params.location);
    if (params.from)     q.set('from',     params.from);
    if (params.to)       q.set('to',       params.to);
    if (params.page)     q.set('page',     String(params.page));
    if (params.pageSize) q.set('pageSize', String(params.pageSize));
    const res = await apiClient.get<DailyParameterLogListResponse>(`${BASE}?${q}`);
    return res.data;
  },

  stats: async (): Promise<DailyParameterLogStats> => {
    const res = await apiClient.get<DailyParameterLogStats>(`${BASE}/stats`);
    return res.data;
  },

  getById: async (id: string): Promise<DailyParameterLog> => {
    const res = await apiClient.get<DailyParameterLog>(`${BASE}/${id}`);
    return res.data;
  },

  getToday: async (location: string): Promise<DailyParameterLog | null> => {
    const res = await apiClient.get<DailyParameterLog | null>(
      `${BASE}/today/${encodeURIComponent(location)}`
    );
    return res.data;
  },

  create: async (payload: CreateDailyParameterLogPayload): Promise<DailyParameterLog> => {
    const res = await apiClient.post<DailyParameterLog>(BASE, payload);
    return res.data;
  },

  update: async (id: string, payload: UpdateDailyParameterLogPayload): Promise<DailyParameterLog> => {
    const res = await apiClient.put<DailyParameterLog>(`${BASE}/${id}`, payload);
    return res.data;
  },

  delete: async (id: string): Promise<void> => {
    await apiClient.delete(`${BASE}/${id}`);
  },
};

export default dailyParameterLogApi