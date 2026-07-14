import { apiClient } from './client';
import type { DieselSupply, DieselDistribution, DieselStockSummary } from '../types';

export const dieselSupplyApi = {
  // ── Bulk supplies ───────────────────────────────────────────────────────────
  listSupplies: (params?: { days?: number; page?: number }) =>
    apiClient
      .get<{ items: DieselSupply[]; totalCount: number }>('/diesel-supply/supplies', { params })
      .then(r => r.data),

  availableSupplies: () =>
    apiClient.get<DieselSupply[]>('/diesel-supply/supplies/available').then(r => r.data),

  createSupply: (data: {
    vendor:            string;
    quantityLitres:    number;
    unitPriceNaira:    number;
    supplyDate?:       string;
    invoiceNumber?:    string;
    storageLocation?:  string;
    deliveryDocuments?:string;
    receivingOfficer?: string;
    notes?:            string;
  }) => apiClient.post<DieselSupply>('/diesel-supply/supplies', data).then(r => r.data),

  // ── Distributions ─────────────────────────────────────────────────────────
  listDistributions: (params?: { distributionType?: string; bulkSupplyId?: string; vehicleRegNo?: string; days?: number; page?: number }) =>
    apiClient
      .get<{ items: DieselDistribution[]; totalCount: number }>('/diesel-supply/distributions', { params })
      .then(r => r.data),

  createDistribution: (data: {
    distributionType:      string;
    bulkSupplyReference:   string;
    quantityLitres:        number;
    distributionDate?:     string;
    purpose?:              string;
    vehicleRegNo?:         string;
    driver?:               string;
    odometerReading?:      string;
    destinationLocation?:  string;
    issuingOfficer?:       string;
    receivingOfficer?:     string;
    recipientAcknowledged?:boolean;
    notes?:                string;
  }) => apiClient.post<DieselDistribution>('/diesel-supply/distributions', data).then(r => r.data),

  deleteDistribution: (id: string) => apiClient.delete(`/diesel-supply/distributions/${id}`),

  summary: () => apiClient.get<DieselStockSummary>('/diesel-supply/summary').then(r => r.data),
};
