import type { APIRequestContext, Locator, Page as Page_ } from '@playwright/test'

/** Decision P2-4: where the React UI lives until the cutover; then ''. */
export const appBasePath = '/app'

/** The URL of a React page, e.g. `app('/Catalog/Details/1')`. */
export function app(path = '/'): string {
  return `${appBasePath}${path}`
}

/**
 * Matches the browser's URL for a React page, query string included in `path` if any. The list is
 * `/app` after a click on a link inside the app and `/app/` when opened directly; both are the list.
 */
export function appUrl(path = '/'): RegExp {
  const escaped = path.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  return new RegExp(`^https?://[^/]+${appBasePath}${path === '/' ? '/?' : escaped}$`)
}

/** A form field by its exact label ("Stock" is also part of "Restock" and "Max stock"). */
export function field(page: Page_, label: string): Locator {
  return page.getByLabel(label, { exact: true })
}

/** Items the browser tests create; the smoke tests' prefix (`smoke-`) plus their own, so each suite sweeps only its items. */
export const itemPrefix = 'smoke-e2e-'

export function newItemName(): string {
  return `${itemPrefix}${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
}

export interface CatalogItem {
  Id: number
  Name: string
  Description: string | null
  Price: number
  PictureFileName: string
  CatalogTypeId: number
  CatalogType: { Id: number; Type: string }
  CatalogBrandId: number
  CatalogBrand: { Id: number; Brand: string }
  AvailableStock: number
  RestockThreshold: number
  MaxStockThreshold: number
}

export interface Page {
  ActualPage: number
  ItemsPerPage: number
  TotalItems: number
  TotalPages: number
  Data: CatalogItem[]
}

const items = '/api/v1/catalog/items'

export async function getPage(request: APIRequestContext, pageSize = 10, pageIndex = 0): Promise<Page> {
  const response = await request.get(`${items}?pageSize=${pageSize}&pageIndex=${pageIndex}`)
  if (!response.ok()) {
    throw new Error(`GET ${response.url()} returned ${response.status()}`)
  }
  return (await response.json()) as Page
}

export async function getItem(request: APIRequestContext, id: number): Promise<CatalogItem | null> {
  const response = await request.get(`${items}/${id}`)
  return response.ok() ? ((await response.json()) as CatalogItem) : null
}

export async function findByName(request: APIRequestContext, name: string): Promise<CatalogItem | null> {
  const all = await getPage(request, 1000)
  return all.Data.find((item) => item.Name === name) ?? null
}

/** Creates a valid item through the API: first brand and type, price 12.34. */
export async function createItem(request: APIRequestContext, name = newItemName()): Promise<CatalogItem> {
  const brands = (await (await request.get('/api/v1/catalog/brands')).json()) as { Id: number }[]
  const types = (await (await request.get('/api/v1/catalog/types')).json()) as { Id: number }[]
  const response = await request.post(items, {
    headers: await xsrfHeader(request),
    data: {
      Name: name,
      Description: 'Created by the browser tests',
      Price: 12.34,
      PictureFileName: 'dummy.png',
      CatalogTypeId: types[0].Id,
      CatalogBrandId: brands[0].Id,
      AvailableStock: 5,
      RestockThreshold: 1,
      MaxStockThreshold: 10,
    },
  })
  if (response.status() !== 201) {
    throw new Error(`POST ${items} returned ${response.status()}: ${await response.text()}`)
  }
  return (await response.json()) as CatalogItem
}

/** Deletes an item through the API; one that is already gone is fine. */
export async function deleteItem(request: APIRequestContext, id: number): Promise<void> {
  await request.get(`${items}/${id}`) // issues the XSRF-TOKEN cookie if this context has none yet
  const response = await request.delete(`${items}/${id}`, { headers: await xsrfHeader(request) })
  if (response.status() !== 204 && response.status() !== 404) {
    throw new Error(`DELETE ${items}/${id} returned ${response.status()}`)
  }
}

/** Removes every item the browser tests created, e.g. after an aborted run. Seeded items are never touched. */
export async function deleteTestItems(request: APIRequestContext): Promise<number> {
  const leftovers = (await getPage(request, 1000)).Data.filter((item) => item.Name?.startsWith(itemPrefix))
  for (const item of leftovers) {
    await deleteItem(request, item.Id)
  }
  return leftovers.length
}

// Decision P2-7: the token of the XSRF-TOKEN cookie (URL-encoded), issued by any GET under /api/v1/catalog.
async function xsrfHeader(request: APIRequestContext): Promise<Record<string, string>> {
  const cookie = (await request.storageState()).cookies.find((c) => c.name === 'XSRF-TOKEN')
  if (!cookie) {
    throw new Error('No XSRF-TOKEN cookie yet: GET an /api/v1/catalog URL first.')
  }
  return { 'X-XSRF-TOKEN': decodeURIComponent(cookie.value) }
}
