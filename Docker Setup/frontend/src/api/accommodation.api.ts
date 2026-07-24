import { apiClient } from './client';
import type {
  AccommodationLog,
  AccommodationListResponse,
  AccommodationStats,
  CreateAccommodationPayload,
} from '../types';

const BASE = '/accommodation';

export interface AccommodationQueryParams {
  guestHouse?: string;
  status?:     string;
  search?:     string;
  from?:       string;   // YYYY-MM-DD
  to?:         string;
  page?:       number;
  pageSize?:   number;
}

const accommodationApi = {
  list: async (params: AccommodationQueryParams = {}): Promise<AccommodationListResponse> => {
    const q = new URLSearchParams();
    if (params.guestHouse) q.set('guestHouse', params.guestHouse);
    if (params.status)     q.set('status',     params.status);
    if (params.search)     q.set('search',     params.search);
    if (params.from)       q.set('from',        params.from);
    if (params.to)         q.set('to',          params.to);
    if (params.page)       q.set('page',        String(params.page));
    if (params.pageSize)   q.set('pageSize',    String(params.pageSize));
    const res = await apiClient.get<AccommodationListResponse>(`${BASE}?${q}`);
    return res.data;
  },

  stats: async (): Promise<AccommodationStats> => {
    const res = await apiClient.get<AccommodationStats>(`${BASE}/stats`);
    return res.data;
  },

  create: async (payload: CreateAccommodationPayload): Promise<AccommodationLog> => {
    const res = await apiClient.post<AccommodationLog>(BASE, payload);
    return res.data;
  },

  update: async (id: string, payload: Partial<CreateAccommodationPayload>): Promise<AccommodationLog> => {
    const res = await apiClient.put<AccommodationLog>(`${BASE}/${id}`, payload);
    return res.data;
  },

  delete: async (id: string): Promise<void> => {
    await apiClient.delete(`${BASE}/${id}`);
  },
};

export default accommodationApi;
