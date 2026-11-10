import { useEffect, useState } from 'react'
import { ApiError, getCatalogItem } from './api.ts'
import type { CatalogItem } from './api.ts'

/** The Razor layout's title: "{ViewBag.Title} - Catalog manager (MVC)". */
export function useDocumentTitle(pageTitle: string) {
  useEffect(() => {
    document.title = `${pageTitle} - Catalog manager (MVC)`
  }, [pageTitle])
}

export type CatalogItemState =
  | { status: 'loading' }
  | { status: 'loaded'; item: CatalogItem }
  | { status: 'failed'; message: string }

export const itemNotFound = 'The catalog item was not found.'

/**
 * Loads the item of a `:id` route parameter. An id that isn't a number never reaches the server and
 * counts as not found, like an unknown one (the Razor UI answered those with empty 400 and 404 pages).
 */
export function useCatalogItem(idParam: string | undefined): CatalogItemState {
  const id = idParam !== undefined && /^\d+$/.test(idParam) ? Number(idParam) : null
  const [result, setResult] = useState<{ id: number; state: CatalogItemState } | null>(null)

  useEffect(() => {
    if (id === null) {
      return
    }
    const controller = new AbortController()
    getCatalogItem(id, controller.signal).then(
      (item) => setResult({ id, state: { status: 'loaded', item } }),
      (error: unknown) => {
        if (!controller.signal.aborted) {
          const notFound = error instanceof ApiError && error.status === 404
          const message = notFound ? itemNotFound : 'The catalog item could not be loaded.'
          setResult({ id, state: { status: 'failed', message } })
        }
      },
    )
    return () => controller.abort()
  }, [id])

  if (id === null) {
    return { status: 'failed', message: itemNotFound }
  }
  // A result for another id is from the route this component showed before.
  return result?.id === id ? result.state : { status: 'loading' }
}
