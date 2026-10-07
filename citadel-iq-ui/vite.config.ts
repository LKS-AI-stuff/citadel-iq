import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The API is reached on the same origin: the dev server proxies the API, sign-in and OIDC callback paths to the
// backend, so the session cookie is first-party and no CORS is involved. changeOrigin stays false so the backend
// builds the OIDC redirect URI from this server's host (register http://localhost:5173/signin-oidc).
const apiTarget = process.env.API_PROXY_TARGET ?? 'http://localhost:5157'
const proxied = { target: apiTarget, changeOrigin: false, secure: false }

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': proxied,
      '/auth': proxied,
      '/signin-oidc': proxied,
      '/signout-callback-oidc': proxied,
    },
  },
})
