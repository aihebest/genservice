import axios from 'axios';
import { useAuthStore } from '../store/authStore';

// In dev: VITE_API_URL is "" → baseURL is "/api/v1" → Vite proxy forwards to the API.
// In production build: VITE_API_URL is the full Azure URL → direct calls.
const API_BASE = import.meta.env.VITE_API_URL ?? '';

export const apiClient = axios.create({
  baseURL: `${API_BASE}/api/v1`,
  headers: { 'Content-Type': 'application/json' },
  timeout: 30_000,
});

// ── Request interceptor: attach JWT token ─────────────────────────────────────
apiClient.interceptors.request.use((config) => {
  const token = useAuthStore.getState().token;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// ── Response interceptor: handle 401 globally ────────────────────────────────
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      useAuthStore.getState().logout();
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);
