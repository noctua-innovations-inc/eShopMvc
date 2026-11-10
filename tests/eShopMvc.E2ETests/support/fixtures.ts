import { test as base } from '@playwright/test'
import { createItem, deleteItem, findByName, newItemName } from './app.ts'
import type { CatalogItem } from './app.ts'

interface Fixtures {
  /** An item owned by the test: created through the API before it, deleted after it. */
  item: CatalogItem
  /** A name for an item the test creates through the UI; whatever has that name is deleted after it. */
  newName: string
}

// Tests that change data work only on their own items, never on seeded ones (as the smoke tests).
export const test = base.extend<Fixtures>({
  item: async ({ request }, use) => {
    const item = await createItem(request)
    await use(item)
    await deleteItem(request, item.Id)
  },
  newName: async ({ request }, use) => {
    const name = newItemName()
    await use(name)
    const created = await findByName(request, name)
    if (created) {
      await deleteItem(request, created.Id)
    }
  },
})

export { expect } from '@playwright/test'
