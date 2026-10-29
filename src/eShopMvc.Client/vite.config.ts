import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// The ASP.NET Core app in Development (src/eShopMvc.Web/Properties/launchSettings.json).
const backend = 'http://localhost:5070'

// Decision P2-3: in development, everything that isn't the React app comes from the ASP.NET Core app.
const proxied = ['/api', '/items', '/health', '/css', '/lib', '/images', '/fonts', '/favicon.ico']

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // Decisions P2-2 and P2-4: served by the ASP.NET Core app from wwwroot/app under /app/.
  base: '/app/',
  build: {
    outDir: '../eShopMvc.Web/wwwroot/app',
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    strictPort: true,
    proxy: Object.fromEntries(proxied.map((path) => [path, backend])),
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
