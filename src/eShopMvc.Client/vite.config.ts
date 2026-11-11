import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// The ASP.NET Core app in Development (src/eShopMvc.Web/Properties/launchSettings.json).
const backend = 'http://localhost:5070'

// Decision P2-3: in development, everything that isn't the React app comes from the ASP.NET Core app.
const proxied = ['/api', '/items', '/health', '/css', '/lib', '/images', '/fonts', '/favicon.ico']

// https://vite.dev/config/
export default defineConfig(({ command }) => ({
  plugins: [react()],
  // Decisions P2-2 and P2-9: the pages have the Razor UI's URLs (/, /Catalog/...), but the built files
  // stay in wwwroot/app and are served under /app/ (the build empties its folder, and wwwroot holds
  // the CSS, images and libraries). The dev server serves the pages from / too.
  base: command === 'build' ? '/app/' : '/',
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
}))
