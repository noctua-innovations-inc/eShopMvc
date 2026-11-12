import type { PaginatedItems } from './api.ts'

/** The list page's URL for a page size and a zero-based page index (P2-9). */
export function pageUrl(pageSize: number, pageIndex: number): string {
  return `/?pageSize=${pageSize}&pageIndex=${pageIndex}`
}

/**
 * Decision P3-9: the index of the last page if the API answered for a page beyond it (a page index
 * typed into the URL, or the last page's only item deleted), otherwise null. An empty catalog has
 * no pages; its "last page" is the first one.
 */
export function lastPageIfBeyond(page: Pick<PaginatedItems<unknown>, 'ActualPage' | 'TotalPages'>): number | null {
  const last = Math.max(page.TotalPages - 1, 0)
  return page.ActualPage > last ? last : null
}
