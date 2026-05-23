import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// In Docker dev the Vite server runs inside the frontend container.
// API_TARGET uses the Docker service name so the proxy can reach the API
// container over the internal Docker network (no CORS needed).
// Outside Docker (plain npm run dev) it falls back to localhost:8080.
const API_TARGET = process.env.API_TARGET ?? 'http://localhost:8080';

export default defineConfig({
  plugins: [react()],

  server: {
    host: '0.0.0.0',   // Accept connections from Docker network / host browser
    port: 5173,

    proxy: {
      // Every /api/* request from the browser is forwarded to the API.
      // The browser never talks to port 8080 directly → no CORS preflight needed.
      '/api': {
        target: API_TARGET,
        changeOrigin: true,
      },
    },
  },
});
