import { defineConfig, devices } from '@playwright/test'

// Decision P2-12: browser tests against a running app, like the smoke tests (ESHOP_BASE_URL; the
// default here is the new app, the only one with the React UI). Playwright does not start the app.
export default defineConfig({
  testDir: './tests',
  globalSetup: './support/globalSetup.ts',
  // One at a time: the list and paging tests read totals that the create and delete tests change.
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  reporter: 'list',
  use: {
    baseURL: process.env.ESHOP_BASE_URL || 'http://localhost:5070',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
