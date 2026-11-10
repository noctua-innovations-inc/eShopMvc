import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router'
import { ApiError, getCatalogItems } from '../api.ts'
import type { CatalogItem, PaginatedItems } from '../api.ts'
import { CatalogTable } from '../components/CatalogTable.tsx'
import { Pager } from '../components/Pager.tsx'

type State =
  | { status: 'loading' }
  | { status: 'loaded'; page: PaginatedItems<CatalogItem> }
  | { status: 'failed'; message: string }

// The Razor Catalog/Index.cshtml: "Create New", the table and the pager, for ?pageSize=…&pageIndex=….
export function CatalogIndex() {
  const [searchParams] = useSearchParams()
  const pageSize = searchParams.get('pageSize')
  const pageIndex = searchParams.get('pageIndex')
  const [state, setState] = useState<State>({ status: 'loading' })

  useEffect(() => {
    document.title = 'Index - Catalog manager (MVC)'
  }, [])

  // The previous page stays on screen until the next one has arrived.
  useEffect(() => {
    const controller = new AbortController()
    getCatalogItems(pageSize, pageIndex, controller.signal).then(
      (page) => setState({ status: 'loaded', page }),
      (error: unknown) => {
        if (!controller.signal.aborted) {
          setState({ status: 'failed', message: failureMessage(error) })
        }
      },
    )
    return () => controller.abort()
  }, [pageSize, pageIndex])

  return (
    <div className="esh-table">
      <p className="esh-link-wrapper">
        {/* Until step 6 this is the Razor page, outside the React app: a plain link. */}
        <a className="btn esh-button esh-button-primary" href="/Catalog/Create">
          Create New
        </a>
      </p>

      {state.status === 'failed' && <p role="alert">{state.message}</p>}
      {state.status === 'loaded' && (
        <>
          <CatalogTable items={state.page.Data} />
          <Pager page={state.page} />
        </>
      )}
    </div>
  )
}

// The Razor UI answered invalid paging with an empty 400 page (R-16); the API still does, and the
// React UI says so.
function failureMessage(error: unknown): string {
  return error instanceof ApiError && error.status === 400
    ? 'Invalid paging values.'
    : 'The catalog could not be loaded.'
}
