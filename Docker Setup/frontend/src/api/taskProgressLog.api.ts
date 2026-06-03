import { apiClient } from './client';
import type { TaskProgressLog, TechnicianSummary } from '../types';

export const taskProgressLogApi = {
  list: (module: string, entityId: string) =>
    apiClient.get<TaskProgressLog[]>('/task-logs', { params: { module, entityId } }).then(r => r.data),

  create: (data: {
    module:            string;
    entityId:          string;
    refNumber:         string;
    taskTitle:         string;
    activityPerformed: string;
    progressStatus:    string;
    materialsRequired?: string;
    nextAction?:       string;
    isProxy?:          boolean;
    proxyForName?:     string;
  }) => apiClient.post<TaskProgressLog>('/task-logs', data).then(r => r.data),

  performance: () =>
    apiClient.get<TechnicianSummary[]>('/task-logs/performance').then(r => r.data),
};
