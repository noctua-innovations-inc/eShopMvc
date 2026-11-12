import { useEffect, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { ApiError, getCatalogItems } from '../api.ts'
import type { CatalogItem, PaginatedItems } from '../api.ts'
import { CatalogTable } from '../components/CatalogTable.tsx'
import { Pager } from '../components/Pager.tsx'
import { useDocumentTitle } from '../hooks.ts'
import { lastPageIfBeyond, pageUrl } from '../paging.ts'

type State =
  | { status: 'loading' }
  | { status: 'loaded'; page: PaginatedItems<CatalogItem> }
  | { status: 'failed'; message: string }

// The Razor Catalog/Index.cshtml: "Create New", the table and the pager, for ?pageSize=…&pageIndex=….
export function CatalogIndex() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const pageSize = searchParams.get('pageSize')
  const pageIndex = searchParams.get('pageIndex')
  const [state, setState] = useState<State>({ status: 'loading' })

  useDocumentTitle('Index')

  // Decision P3-4: only the list page fills the window (site.css, "esh-fill"); the other pages keep
  // their layout.
  useEffect(() => {
    document.body.classList.add('esh-fill')
    return () => document.body.classList.remove('esh-fill')
  }, [])

  // The previous page stays on screen until the next one has arrived.
  useEffect(() => {
    const controller = new AbortController()
    getCatalogItems(pageSize, pageIndex, controller.signal).then(
      (page) => {
        // Decision P3-9: a page index beyond the last page (typed into the URL, or the last page's
        // only item deleted) shows the last page instead of an empty one. The URL is replaced, so
        // Back does not return to the empty page, and this effect loads the last page.
        const lastPage = lastPageIfBeyond(page)
        if (lastPage !== null) {
          void navigate(pageUrl(page.ItemsPerPage, lastPage), { replace: true })
          return
        }
        setState({ status: 'loaded', page })
      },
      (error: unknown) => {
        if (!controller.signal.aborted) {
          setState({ status: 'failed', message: failureMessage(error) })
        }
      },
    )
    return () => controller.abort()
  }, [pageSize, pageIndex, navigate])

  return (
    <div className="esh-table">
      <p className="esh-link-wrapper">
        <Link className="btn esh-button esh-button-primary" to="/Catalog/Create">
          Create New
        </Link>
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
