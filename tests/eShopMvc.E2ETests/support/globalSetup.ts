import { request } from '@playwright/test'
import type { FullConfig } from '@playwright/test'
import { deleteTestItems } from './app.ts'

// Runs once before the tests: fails fast with a clear message if the app isn't reachable, and deletes
// smoke-e2e-* items left behind by an aborted earlier run (the smoke tests' SuiteFixture does the same).
export default async function globalSetup(config: FullConfig) {
  const baseURL = config.projects[0].use.baseURL!
  const context = await request.newContext({ baseURL })
  try {
    try {
      await context.get('/health/live')
    } catch (error) {
      throw new Error(
        `The app under test is not reachable at ${baseURL}. Start it first (npm run build in ` +
          `src/eShopMvc.Client, then dotnet run --project src/eShopMvc.Web), or set ESHOP_BASE_URL. ${String(error)}`,
      )
    }
    await deleteTestItems(context)
  } finally {
    await context.dispose()
  }
}
