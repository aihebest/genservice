import { apiClient } from './client';
import type {
  GeneratorLog, GeneratorLogListResponse,
  DieselRecord, DieselRecordListResponse,
  FuelPowerSummary,
} from '../types';

interface GenListParams {
  location?:  string;
  status?:    string;
  runReason?: string;
  from?:      string;
  to?:        string;
  page?:      number;
  pageSize?:  number;
}

interface DieselListParams {
  recordType?: string;
  from?:       string;
  to?:         string;
  page?:       number;
  pageSize?:   number;
}

export const fuelApi = {
  // ── Summary ───────────────────────────────────────────────────────────────
  summary: () =>
    apiClient.get<FuelPowerSummary>('/fuel/summary').then(r => r.data),

  // ── Generator ─────────────────────────────────────────────────────────────
  listGeneratorLogs: (params?: GenListParams) =>
    apiClient.get<GeneratorLogListResponse>('/fuel/generator', { params }).then(r => r.data),

  startGenerator: (data: {
    location: string; runReason: string; outageCause?: string;
    fuelLevelBefore?: number; notes?: string;
  }) =>
    apiClient.post<GeneratorLog>('/fuel/generator/start', data).then(r => r.data),

  stopGenerator: (id: string, data: { fuelLevelAfter?: number; notes?: string }) =>
    apiClient.post<GeneratorLog>(`/fuel/generator/${id}/stop`, data).then(r => r.data),

  // ── Diesel ────────────────────────────────────────────────────────────────
  listDieselRecords: (params?: DieselListParams) =>
    apiClient.get<DieselRecordListResponse>('/fuel/diesel', { params }).then(r => r.data),

  createDieselRecord: (data: {
    recordDate: string; recordType: string; quantityLitres: number;
    unitCostNaira: number; supplier?: string; destination?: string; notes?: string;
  }) =>
    apiClient.post<DieselRecord>('/fuel/diesel', data).then(r => r.data),

  approveDieselRecord: (id: string) =>
    apiClient.post<DieselRecord>(`/fuel/diesel/${id}/approve`).then(r => r.data),

  deleteDieselRecord: (id: string) =>
    apiClient.delete(`/fuel/diesel/${id}`),
};
