import { vi } from 'vitest'
import type { CatalogItem, PaginatedItems } from '../api.ts'

type Reply = { status: number; body?: unknown }

/**
 * Replaces `fetch` for one test: `routes` maps a URL (path and query; other methods than GET as
 * "DELETE /url") to its reply; anything else gets a 404. Returns the mock, so tests can assert on
 * the requests.
 */
export function stubFetch(routes: Record<string, Reply>) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const method = init?.method ?? 'GET'
    const reply = routes[method === 'GET' ? String(input) : `${method} ${String(input)}`] ?? { status: 404 }
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

/** The one request sent with `method`: its URL, headers and parsed JSON body. */
export function sentRequest(fetchMock: ReturnType<typeof stubFetch>, method: string) {
  const calls = fetchMock.mock.calls.filter(([, init]) => init?.method === method)
  if (calls.length !== 1) {
    throw new Error(`Expected one ${method} request, found ${calls.length}.`)
  }
  const [input, init] = calls[0]
  return {
    url: String(input),
    headers: init?.headers as Record<string, string>,
    body: typeof init?.body === 'string' ? (JSON.parse(init.body) as Record<string, unknown>) : undefined,
  }
}

export const brands = [
  { Id: 1, Brand: 'Azure' },
  { Id: 2, Brand: '.NET' },
]

export const types = [
  { Id: 1, Type: 'Mug' },
  { Id: 2, Type: 'T-Shirt' },
]

/** The GETs of the forms' drop-downs. */
export const listRoutes = {
  '/api/v1/catalog/brands': { status: 200, body: brands },
  '/api/v1/catalog/types': { status: 200, body: types },
}

export const session ={ MachineName: 'TESTBOX', SessionStartTime: '10/29/2026 9:05:07 AM' }

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
