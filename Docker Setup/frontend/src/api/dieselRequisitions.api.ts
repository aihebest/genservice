import { apiClient } from './client';
import type {
  DieselRequisition,
  DieselRequisitionListResponse,
  DieselRequisitionStats,
  CreateDieselRequisitionPayload,
  DispenseDieselPayload,
} from '../types';

const BASE = '/diesel/requisitions';

export const dieselRequisitionsApi = {
  list: (params?: {
    status?:    string;
    equipType?: string;
    location?:  string;
    requester?: string;
    from?:      string;
    to?:        string;
    page?:      number;
    pageSize?:  number;
  }) =>
    apiClient
      .get<DieselRequisitionListResponse>(BASE, { params })
      .then((r) => r.data),

  stats: () =>
    apiClient.get<DieselRequisitionStats>(`${BASE}/stats`).then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<DieselRequisition>(`${BASE}/${id}`).then((r) => r.data),

  create: (payload: CreateDieselRequisitionPayload) =>
    apiClient.post<DieselRequisition>(BASE, payload).then((r) => r.data),

  approve: (id: string, notes?: string) =>
    apiClient
      .post<DieselRequisition>(`${BASE}/${id}/approve`, { notes })
      .then((r) => r.data),

  reject: (id: string, reason: string) =>
    apiClient
      .post<DieselRequisition>(`${BASE}/${id}/reject`, { reason })
      .then((r) => r.data),

  dispense: (id: string, payload: DispenseDieselPayload) =>
    apiClient
      .post<DieselRequisition>(`${BASE}/${id}/dispense`, payload)
      .then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete(`${BASE}/${id}`).then((r) => r.data),
};
