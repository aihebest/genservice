import { apiClient } from './client';
import type { AuditEntry } from '../types';

export const auditApi = {
  list: (params: {
    entityType?: string;
    entityId?:   string;
    refNumber?:  string;
    days?:       number;
    page?:       number;
    pageSize?:   number;
  }) => apiClient.get<{ items: AuditEntry[]; totalCount: number }>('/audit', { params }).then(r => r.data),
};
