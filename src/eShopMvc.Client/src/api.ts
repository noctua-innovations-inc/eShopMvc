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

/** The fields the forms send (P2-6: the server copies only these; `Id` only matters for PUT). */
export interface CatalogItemInput {
  Id?: number
  Name: string | null
  Description: string | null
  Price: number
  PictureFileName?: string | null
  CatalogTypeId: number
  CatalogBrandId: number
  AvailableStock: number
  RestockThreshold: number
  MaxStockThreshold: number
  /** Not on the forms; Edit sends the stored value back so that it survives (P2-17). */
  OnReorder?: boolean
}

/** `errors` of a 400 `ValidationProblemDetails`: messages by field name. */
export type ValidationErrors = Record<string, string[]>

/** A response with a status code other than 2xx. */
export class ApiError extends Error {
  readonly status: number
  /** Set when the response was a validation problem. */
  readonly errors: ValidationErrors | null

  constructor(status: number, url: string, errors: ValidationErrors | null = null) {
    super(`${url} returned ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.errors = errors
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

/**
 * One item with its brand, type and picture URL; 404 as an `ApiError`. The id is the page URL's,
 * as it is: the server alone decides whether it is one (P2-15), and answers 404 if not.
 */
export function getCatalogItem(id: string, signal?: AbortSignal): Promise<CatalogItem> {
  return getJson(`/api/v1/catalog/items/${encodeURIComponent(id)}`, signal)
}

export function getCatalogBrands(signal?: AbortSignal): Promise<CatalogBrand[]> {
  return getJson('/api/v1/catalog/brands', signal)
}

export function getCatalogTypes(signal?: AbortSignal): Promise<CatalogType[]> {
  return getJson('/api/v1/catalog/types', signal)
}

/** Creates an item; a 400 with field messages as an `ApiError` with `errors`. */
export function createCatalogItem(item: CatalogItemInput): Promise<void> {
  return write('POST', '/api/v1/catalog/items', item)
}

/** Updates an item; 400 as for create, 404 when it no longer exists. */
export function updateCatalogItem(id: number, item: CatalogItemInput): Promise<void> {
  return write('PUT', `/api/v1/catalog/items/${id}`, { ...item, Id: id })
}

/** Deletes an item; 404 (already gone, R-15) and 400 (antiforgery, P2-7) as an `ApiError`. */
export function deleteCatalogItem(id: number): Promise<void> {
  return write('DELETE', `/api/v1/catalog/items/${id}`)
}

async function write(method: string, url: string, body?: unknown): Promise<void> {
  const headers = writeHeaders()
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }
  const response = await fetch(url, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
  if (!response.ok) {
    throw new ApiError(response.status, url, await readValidationErrors(response))
  }
}

// Only a validation problem has `errors`; other 400s (antiforgery: empty body) and 404s have none.
async function readValidationErrors(response: Response): Promise<ValidationErrors | null> {
  if (response.status !== 400) {
    return null
  }
  try {
    const problem = (await response.json()) as { errors?: ValidationErrors }
    return problem.errors ?? null
  } catch {
    return null
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
