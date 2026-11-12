import { Link } from 'react-router'
import type { PaginatedItems } from '../api.ts'
import { pageUrl } from '../paging.ts'

type PageInfo = Pick<PaginatedItems<unknown>, 'ActualPage' | 'ItemsPerPage' | 'TotalItems' | 'TotalPages' | 'Data'>

// The Razor pager: Previous/Next are always rendered, only hidden by a class when there is no such
// page. Decision P3-9: "Showing" prints the number of rows on the page; the Razor UI printed the page
// size ("Showing 10 of 12" on a last page of two rows; R-24 had kept that).
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
              {` Showing ${page.Data.length} of ${page.TotalItems} products - Page ${page.ActualPage + 1} - ${page.TotalPages} `}
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
