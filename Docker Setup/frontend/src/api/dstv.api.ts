import { apiClient } from './client';
import type { DstvSubscription } from '../types';

export const dstvApi = {
  list: (params?: { status?: string; location?: string; page?: number }) =>
    apiClient
      .get<{ items: DstvSubscription[]; totalCount: number }>('/dstv', { params })
      .then(r => r.data),

  upcoming: (days = 30) =>
    apiClient.get<DstvSubscription[]>('/dstv/upcoming', { params: { days } }).then(r => r.data),

  create: (data: {
    decoderNumber:      string;
    location:           string;
    package:            string;
    amountNaira:        number;
    startDate?:         string;
    endDate?:           string;
    durationMonths?:    number;
    paymentMethod?:     string;
    vendor?:            string;
    receiptAttachment?: string;
    notes?:             string;
  }) => apiClient.post<DstvSubscription>('/dstv', data).then(r => r.data),

  // Manager-only correction of an existing subscription.
  update: (id: string, data: {
    decoderNumber:      string;
    location:           string;
    package:            string;
    amountNaira:        number;
    startDate?:         string;
    endDate?:           string;
    durationMonths?:    number;
    paymentMethod?:     string;
    vendor?:            string;
    receiptAttachment?: string;
    notes?:             string;
  }) => apiClient.put<DstvSubscription>(`/dstv/${id}`, data).then(r => r.data),

  renew: (id: string, data: {
    durationMonths:     number;
    amountNaira:        number;
    renewalDate?:       string;
    paymentMethod?:     string;
    vendor?:            string;
    receiptAttachment?: string;
    notes?:             string;
  }) => apiClient.post<DstvSubscription>(`/dstv/${id}/renew`, data).then(r => r.data),

  remove: (id: string) => apiClient.delete(`/dstv/${id}`),
};
