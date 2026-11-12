import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import { Pager } from './Pager.tsx'

const hidden = 'esh-pager-item--hidden'

function renderPager(actualPage: number, totalPages: number, itemsPerPage = 10, totalItems = 12, rows = itemsPerPage) {
  render(
    <MemoryRouter>
      <Pager
        page={{
          ActualPage: actualPage,
          ItemsPerPage: itemsPerPage,
          TotalItems: totalItems,
          TotalPages: totalPages,
          Data: Array.from({ length: rows }),
        }}
      />
    </MemoryRouter>,
  )
  return {
    previous: screen.getByRole('link', { name: 'Previous' }),
    next: screen.getByRole('link', { name: 'Next' }),
  }
}

describe('Pager', () => {
  it('prints the rows on the page, the total, the one-based page and the page count', () => {
    renderPager(0, 2)

    expect(screen.getByText('Showing 10 of 12 products - Page 1 - 2')).toHaveClass('esh-pager-item')
  })

  it('prints the number of rows, not the page size, on a shorter last page (P3-9)', () => {
    renderPager(1, 2, 10, 12, 2)

    expect(screen.getByText('Showing 2 of 12 products - Page 2 - 2')).toBeInTheDocument()
  })

  it('hides Previous on the first page but still renders it', () => {
    const { previous, next } = renderPager(0, 2)

    expect(previous).toHaveClass('esh-pager-item', 'esh-pager-item--navigable', hidden)
    expect(previous).toHaveAttribute('href', '/?pageSize=10&pageIndex=-1')
    expect(next).not.toHaveClass(hidden)
    expect(next).toHaveAttribute('href', '/?pageSize=10&pageIndex=1')
  })

  it('shows both links on a middle page and keeps the page size', () => {
    const { previous, next } = renderPager(1, 4, 3)

    expect(previous).not.toHaveClass(hidden)
    expect(previous).toHaveAttribute('href', '/?pageSize=3&pageIndex=0')
    expect(next).not.toHaveClass(hidden)
    expect(next).toHaveAttribute('href', '/?pageSize=3&pageIndex=2')
  })

  it('hides Next on the last page', () => {
    const { previous, next } = renderPager(1, 2)

    expect(previous).not.toHaveClass(hidden)
    expect(next).toHaveClass(hidden)
  })

  it('hides both links when there are no items', () => {
    const { previous, next } = renderPager(0, 0, 10, 0, 0)

    expect(previous).toHaveClass(hidden)
    expect(next).toHaveClass(hidden)
    expect(screen.getByText('Showing 0 of 0 products - Page 1 - 0')).toBeInTheDocument()
  })
})
