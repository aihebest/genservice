import { apiClient } from './client';
import type { LoginRequest, LoginResponse } from '../types';

export const authApi = {
  login: (data: LoginRequest) =>
    apiClient.post<LoginResponse>('/auth/login', data).then((r) => r.data),

  /** Exchange a Microsoft Entra ID token for a platform JWT. */
  entraLogin: (idToken: string) =>
    apiClient.post<LoginResponse>('/auth/entra-login', { idToken }).then((r) => r.data),

  me: () =>
    apiClient.get('/auth/me').then((r) => r.data),
};
