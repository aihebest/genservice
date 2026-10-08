import { apiClient } from './client';
import type { ElectricityPurchase, ElectricityBalance } from '../types';

export const electricityApi = {
  list: (params?: { purchaseType?: string; location?: string; days?: number; page?: number }) =>
    apiClient
      .get<{ items: ElectricityPurchase[]; totalCount: number }>('/electricity', { params })
      .then(r => r.data),

  balances: () =>
    apiClient.get<ElectricityBalance[]>('/electricity/balances').then(r => r.data),

  create: (data: {
    purchaseType:            string;
    location:                string;
    amountNaira:             number;
    unitsKwh:                number;
    purchaseDate?:           string;
    vendor?:                 string;
    paymentReference?:       string;
    tokenNumber?:            string;
    meterReadingKwh?:        number;
    receiptAttachment?:      string;
    lowBalanceThresholdKwh?: number;
    notes?:                  string;
    previousMeterReading?:   number;
    currentMeterReading?:    number;
  }) => apiClient.post<ElectricityPurchase>('/electricity', data).then(r => r.data),

  // Manager-only correction of an existing purchase.
  update: (id: string, data: {
    purchaseType:            string;
    location:                string;
    amountNaira:             number;
    unitsKwh:                number;
    purchaseDate?:           string;
    vendor?:                 string;
    paymentReference?:       string;
    tokenNumber?:            string;
    meterReadingKwh?:        number;
    receiptAttachment?:      string;
    lowBalanceThresholdKwh?: number;
    notes?:                  string;
    previousMeterReading?:   number;
    currentMeterReading?:    number;
  }) => apiClient.put<ElectricityPurchase>(`/electricity/${id}`, data).then(r => r.data),

  remove: (id: string) => apiClient.delete(`/electricity/${id}`),

  /** Latest PHED meter reading at a location — pre-fills "Previous reading". */
  lastPhedReading: (location: string) =>
    apiClient.get<{ reading?: number | null; date?: string | null }>('/electricity/last-phed-reading', { params: { location } })
      .then(r => r.data),
};
