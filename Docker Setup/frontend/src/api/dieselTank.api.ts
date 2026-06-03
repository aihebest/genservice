import { apiClient } from './client';
import type { DieselTankReading } from '../types';

export const dieselTankApi = {
  list: (params?: { location?: string; tankIdentifier?: string; days?: number; page?: number }) =>
    apiClient.get<{ items: DieselTankReading[]; totalCount: number }>('/diesel-tank', { params }).then(r => r.data),

  summary: (days = 30) =>
    apiClient.get<Record<string, unknown>>('/diesel-tank/summary', { params: { days } }).then(r => r.data),

  create: (data: {
    location:         string;
    tankIdentifier:   string;
    tankLevelLitres:  number;
    costPerLitreNaira?:number;
    notes?:           string;
  }) => apiClient.post<DieselTankReading>('/diesel-tank', data).then(r => r.data),
};
