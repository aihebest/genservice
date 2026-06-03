import { apiClient } from './client';
import type { GeneratorDailyReading, GeneratorSummary, PowerMeterReading } from '../types';

export const generatorMonitoringApi = {
  // Generator daily readings
  listReadings: (params?: { location?: string; assetNo?: string; days?: number; page?: number }) =>
    apiClient.get<{ items: GeneratorDailyReading[]; totalCount: number }>('/generator-monitoring/readings', { params }).then(r => r.data),

  summary: () =>
    apiClient.get<GeneratorSummary[]>('/generator-monitoring/summary').then(r => r.data),

  alerts: () =>
    apiClient.get<GeneratorDailyReading[]>('/generator-monitoring/alerts').then(r => r.data),

  createReading: (data: {
    assetNo:              string;
    assetDescription:     string;
    location:             string;
    cumulativeRunHours:   number;
    runHoursToday:        number;
    generatorStatus:      string;
    fuelLevelLitres:      number;
    fuelConsumedLitres?:  number;
    utilityAvailableHours?:number;
    serviceIntervalHours: number;
    lastServicedAtHours?: number;
    notes?:               string;
  }) => apiClient.post<GeneratorDailyReading>('/generator-monitoring/readings', data).then(r => r.data),

  deleteReading: (id: string) =>
    apiClient.delete(`/generator-monitoring/readings/${id}`),

  // Power meter readings
  listPowerReadings: (params?: { location?: string; days?: number; page?: number }) =>
    apiClient.get<{ items: PowerMeterReading[]; totalCount: number }>('/power-meter', { params }).then(r => r.data),

  createPowerReading: (data: {
    location:              string;
    meterNumber:           string;
    meterReadingKwh:       number;
    utilityAvailableHours?:number;
    costPerKwhNaira?:      number;
    notes?:                string;
  }) => apiClient.post<PowerMeterReading>('/power-meter', data).then(r => r.data),
};
