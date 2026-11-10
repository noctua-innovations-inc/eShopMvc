import { vi } from 'vitest'
import type { CatalogItem, PaginatedItems } from '../api.ts'

type Reply = { status: number; body?: unknown }

/**
 * Replaces `fetch` for one test: `routes` maps a URL (path and query) to its reply; any other URL
 * gets a 404. Returns the mock, so tests can assert on the requested URLs.
 */
export function stubFetch(routes: Record<string, Reply>) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const reply = routes[String(input)] ?? { status: 404 }
    return new Response(reply.body === undefined ? null : JSON.stringify(reply.body), {
      status: reply.status,
      headers: { 'Content-Type': 'application/json' },
    })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

export function requestedUrls(fetchMock: ReturnType<typeof stubFetch>): string[] {
  return fetchMock.mock.calls.map(([input]) => String(input))
}

export const session = { MachineName: 'TESTBOX', SessionStartTime: '10/29/2026 9:05:07 AM' }

export function catalogItem(overrides: Partial<CatalogItem> = {}): CatalogItem {
  return {
    Id: 1,
    Name: '.NET Bot Black Hoodie',
    Description: '.NET Bot Black Hoodie, and more',
    Price: 19.5,
    PictureFileName: '1.png',
    PictureUri: 'http://localhost/items/1/pic',
    CatalogTypeId: 2,
    CatalogType: { Id: 2, Type: 'T-Shirt' },
    CatalogBrandId: 2,
    CatalogBrand: { Id: 2, Brand: '.NET' },
    AvailableStock: 100,
    RestockThreshold: 0,
    MaxStockThreshold: 0,
    OnReorder: false,
    ...overrides,
  }
}

export function page(overrides: Partial<PaginatedItems<CatalogItem>> = {}): PaginatedItems<CatalogItem> {
  return { ActualPage: 0, ItemsPerPage: 10, TotalItems: 12, TotalPages: 2, Data: [catalogItem()], ...overrides }
}
