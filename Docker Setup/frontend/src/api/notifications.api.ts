import { apiClient } from './client';
import type { NotificationsResponse } from '../types';

export const notificationsApi = {
  list: (unreadOnly = false, take = 20) =>
    apiClient.get<NotificationsResponse>('/notifications', { params: { unreadOnly, take } }).then(r => r.data),

  markRead: (id: string) =>
    apiClient.patch(`/notifications/${id}/read`),

  markAllRead: () =>
    apiClient.patch('/notifications/read-all'),
};
