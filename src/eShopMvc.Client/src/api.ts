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

/** The footer values; the first call starts the session (P2-8). */
export function getSession(signal?: AbortSignal): Promise<SessionInfo> {
  return getJson('/api/v1/session', signal)
}
