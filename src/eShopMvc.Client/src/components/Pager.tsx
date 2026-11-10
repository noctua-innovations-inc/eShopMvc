import { Link } from 'react-router'
import type { PaginatedItems } from '../api.ts'

type PageInfo = Pick<PaginatedItems<unknown>, 'ActualPage' | 'ItemsPerPage' | 'TotalItems' | 'TotalPages'>

// The Razor pager as it was, quirks included (R-24): "Showing" prints the page size, not the number of
// rows, and Previous/Next are always rendered, only hidden by a class when there is no such page.
export function Pager({ page }: { page: PageInfo }) {
  const hasPrevious = page.ActualPage > 0
  const hasNext = page.ActualPage < page.TotalPages - 1

  return (
    <div className="esh-pager">
      <div className="container">
        <article className="esh-pager-wrapper row">
          <nav>
            <Link className={pagerLinkClass(hasPrevious)} to={pageUrl(page.ItemsPerPage, page.ActualPage - 1)}>
              Previous
            </Link>{' '}
            {/* One text node, with the spaces the Razor markup's line breaks left inside the span: they set the gap before "Next". */}
            <span className="esh-pager-item">
              {` Showing ${page.ItemsPerPage} of ${page.TotalItems} products - Page ${page.ActualPage + 1} - ${page.TotalPages} `}
            </span>{' '}
            <Link className={pagerLinkClass(hasNext)} to={pageUrl(page.ItemsPerPage, page.ActualPage + 1)}>
              Next
            </Link>
          </nav>
        </article>
      </div>
    </div>
  )
}

function pagerLinkClass(navigable: boolean): string {
  const base = 'esh-pager-item esh-pager-item--navigable'
  return navigable ? base : `${base} esh-pager-item--hidden`
}

function pageUrl(pageSize: number, pageIndex: number): string {
  return `/?pageSize=${pageSize}&pageIndex=${pageIndex}`
}
