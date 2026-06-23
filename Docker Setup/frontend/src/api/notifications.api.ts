import { apiClient } from './client';
import type { NotificationsResponse } from '../types';

export const notificationsApi = {
  list: (params?: {
    unreadOnly?: boolean;
    take?:       number;
    page?:       number;
    module?:     string;
    type?:       string;
  }) =>
    apiClient
      .get<NotificationsResponse>('/notifications', { params })
      .then(r => r.data),

  markRead: (id: string) =>
    apiClient.patch(`/notifications/${id}/read`),

  markUnread: (id: string) =>
    apiClient.patch(`/notifications/${id}/unread`),

  markAllRead: () =>
    apiClient.patch('/notifications/read-all'),

  delete: (id: string) =>
    apiClient.delete(`/notifications/${id}`),

  clearRead: () =>
    apiClient.delete('/notifications/clear-read').then(r => r.data),

  modules: () =>
    apiClient.get<string[]>('/notifications/modules').then(r => r.data),
};
