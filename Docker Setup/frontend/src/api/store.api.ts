import { apiClient } from './client';
import type {
  StoreItemListResponse,
  StoreItem,
  StoreMovement,
  StoreRequisitionListResponse,
  StoreRequisition,
  CreateStoreItemPayload,
  UpdateStoreItemPayload,
  RestockPayload,
  AdjustStockPayload,
  CreateStoreRequisitionPayload,
  IssueRequisitionPayload,
} from '../types';

const BASE_ITEMS = '/store/items';
const BASE_REQS  = '/store/requisitions';

// ── Store Items ───────────────────────────────────────────────────────────────

export const storeItemsApi = {
  list: (params?: {
    search?:   string;
    category?: string;
    lowStock?: boolean;
    isActive?: boolean;
    page?:     number;
    pageSize?: number;
  }) =>
    apiClient
      .get<StoreItemListResponse>(BASE_ITEMS, { params })
      .then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<StoreItem>(`${BASE_ITEMS}/${id}`).then((r) => r.data),

  movements: (id: string, limit = 50) =>
    apiClient
      .get<StoreMovement[]>(`${BASE_ITEMS}/${id}/movements`, { params: { limit } })
      .then((r) => r.data),

  allMovements: (params?: { itemCode?: string; type?: string; limit?: number }) =>
    apiClient
      .get<StoreMovement[]>('/store/movements', { params })
      .then((r) => r.data),

  create: (payload: CreateStoreItemPayload) =>
    apiClient.post<StoreItem>(BASE_ITEMS, payload).then((r) => r.data),

  update: (id: string, payload: UpdateStoreItemPayload) =>
    apiClient.put<StoreItem>(`${BASE_ITEMS}/${id}`, payload).then((r) => r.data),

  restock: (id: string, payload: RestockPayload) =>
    apiClient.post<StoreItem>(`${BASE_ITEMS}/${id}/restock`, payload).then((r) => r.data),

  adjust: (id: string, payload: AdjustStockPayload) =>
    apiClient.post<StoreItem>(`${BASE_ITEMS}/${id}/adjust`, payload).then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete(`${BASE_ITEMS}/${id}`).then((r) => r.data),
};

// ── Store Requisitions ────────────────────────────────────────────────────────

export const storeRequisitionsApi = {
  list: (params?: {
    status?:     string;
    requester?:  string;
    department?: string;
    page?:       number;
    pageSize?:   number;
  }) =>
    apiClient
      .get<StoreRequisitionListResponse>(BASE_REQS, { params })
      .then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<StoreRequisition>(`${BASE_REQS}/${id}`).then((r) => r.data),

  create: (payload: CreateStoreRequisitionPayload) =>
    apiClient.post<StoreRequisition>(BASE_REQS, payload).then((r) => r.data),

  approve: (id: string, notes?: string) =>
    apiClient
      .post<StoreRequisition>(`${BASE_REQS}/${id}/approve`, { notes })
      .then((r) => r.data),

  reject: (id: string, reason: string) =>
    apiClient
      .post<StoreRequisition>(`${BASE_REQS}/${id}/reject`, { reason })
      .then((r) => r.data),

  issue: (id: string, payload: IssueRequisitionPayload) =>
    apiClient
      .post<StoreRequisition>(`${BASE_REQS}/${id}/issue`, payload)
      .then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete(`${BASE_REQS}/${id}`).then((r) => r.data),
};
