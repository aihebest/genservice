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
    assetNo:                 string;
    assetDescription:        string;
    location:                string;
    currentEngineReading:    number;
    generatorStatus:         string;
    currentFuelLevelLitres:  number;
    fuelAddedLitres?:        number;
    fuelRemovedLitres?:      number;
    previousEngineReading?:  number;
    previousFuelLevelLitres?:number;
    previousUtilityReading?: number;
    currentUtilityReading?:  number;
    serviceIntervalHours:    number;
    serviceCompleted?:       boolean;
    lastServicedAtHours?:    number;
    notes?:                  string;
    readingDate?:            string;
  }) => apiClient.post<GeneratorDailyReading>('/generator-monitoring/readings', data).then(r => r.data),

  deleteReading: (id: string) =>
    apiClient.delete(`/generator-monitoring/readings/${id}`),

  // Power meter readings
  listPowerReadings: (params?: { location?: string; days?: number; page?: number }) =>
    apiClient.get<{ items: PowerMeterReading[]; totalCount: number }>('/power-meter', { params }).then(r => r.data),

  createPowerReading: (data: {
    location:              string;
    meterNumber:           string;
    previousMeterReading:  number;
    currentMeterReading:   number;
    readingDate?:          string;
    utilityAvailableHours?:number;
    notes?:                string;
  }) => apiClient.post<PowerMeterReading>('/power-meter', data).then(r => r.data),
};
