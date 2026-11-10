// The JSON API of the ASP.NET Core app (Decisions P2-6 and P2-8). Property names are PascalCase (R-7).

export interface CatalogBrand {
  Id: number
  Brand: string | null
}

export interface CatalogType {
  Id: number
  Type: string | null
}

export interface CatalogItem {
  Id: number
  Name: string | null
  Description: string | null
  Price: number
  PictureFileName: string | null
  PictureUri: string | null
  CatalogTypeId: number
  CatalogType: CatalogType | null
  CatalogBrandId: number
  CatalogBrand: CatalogBrand | null
  AvailableStock: number
  RestockThreshold: number
  MaxStockThreshold: number
  OnReorder: boolean
}

export interface PaginatedItems<T> {
  ActualPage: number
  ItemsPerPage: number
  TotalItems: number
  TotalPages: number
  Data: T[]
}

export interface SessionInfo {
  MachineName: string
  SessionStartTime: string
}

/** A response with a status code other than 2xx. */
export class ApiError extends Error {
  readonly status: number

  constructor(status: number, url: string) {
    super(`${url} returned ${status}`)
    this.name = 'ApiError'
    this.status = status
  }
}

async function getJson<T>(url: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(url, { headers: { Accept: 'application/json' }, signal })
  if (!response.ok) {
    throw new ApiError(response.status, url)
  }
  return (await response.json()) as T
}

/**
 * One page of the catalog. The paging values go to the server as they are in the page URL (null: the
 * server's default, 10 and 0), so the server alone decides what is valid (R-16: 400).
 */
export function getCatalogItems(
  pageSize: string | null,
  pageIndex: string | null,
  signal?: AbortSignal,
): Promise<PaginatedItems<CatalogItem>> {
  const query = new URLSearchParams()
  if (pageSize !== null) {
    query.set('pageSize', pageSize)
  }
  if (pageIndex !== null) {
    query.set('pageIndex', pageIndex)
  }
  const suffix = query.size > 0 ? `?${query}` : ''
  return getJson(`/api/v1/catalog/items${suffix}`, signal)
}

/** One item with its brand, type and picture URL; 404 as an `ApiError`. */
export function getCatalogItem(id: number, signal?: AbortSignal): Promise<CatalogItem> {
  return getJson(`/api/v1/catalog/items/${id}`, signal)
}

/** Deletes an item; 404 (already gone, R-15) and 400 (antiforgery, P2-7) as an `ApiError`. */
export async function deleteCatalogItem(id: number): Promise<void> {
  const url = `/api/v1/catalog/items/${id}`
  const response = await fetch(url, { method: 'DELETE', headers: writeHeaders() })
  if (!response.ok) {
    throw new ApiError(response.status, url)
  }
}

// Decision P2-7: every GET under /api/v1/catalog issues the XSRF-TOKEN cookie (URL-encoded by
// ASP.NET Core); writes send its value back in the X-XSRF-TOKEN header. Without the cookie the header
// is left out and the server answers 400.
function writeHeaders(): Record<string, string> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  const cookie = document.cookie.split('; ').find((c) => c.startsWith('XSRF-TOKEN='))
  if (cookie) {
    headers['X-XSRF-TOKEN'] = decodeURIComponent(cookie.slice('XSRF-TOKEN='.length))
  }
  return headers
}

/** The footer values; the first call starts the session (P2-8). */
export function getSession(signal?: AbortSignal): Promise<SessionInfo> {
  return getJson('/api/v1/session', signal)
}
