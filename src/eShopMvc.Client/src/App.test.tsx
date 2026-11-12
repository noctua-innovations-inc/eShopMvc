import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import App from './App.tsx'
import { catalogItem, page, requestedUrls, stubFetch } from './test/fakeApi.ts'

const items = '/api/v1/catalog/items'

function renderApp(url: string) {
  render(
    <MemoryRouter initialEntries={[url]}>
      <App />
    </MemoryRouter>,
  )
}

describe('App', () => {
  it.each(['/', '/Catalog', '/Catalog/Index'])('shows the catalog list in the shell at %s', async (url) => {
    stubFetch({ [items]: { status: 200, body: page() } })

    renderApp(url)

    // The grid's rows are divs with role "row" (P3-5); cells with a component arrive a moment later (P3-6).
    const row = (await screen.findByText('.NET Bot Black Hoodie')).closest<HTMLElement>('[role="row"]')!
    expect(await within(row).findByText('$19.50')).toHaveClass('esh-table-price')
    expect(within(row).getByText('.NET')).toBeInTheDocument()
    expect(within(row).getByText('T-Shirt')).toBeInTheDocument()
    expect(within(row).getByRole('img')).toHaveAttribute('src', 'http://localhost/items/1/pic')
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Catalog manager (MVC)')
    expect(document.title).toBe('Index - Catalog manager (MVC)')
  })

  it('uses the Razor column labels', async () => {
    stubFetch({ [items]: { status: 200, body: page() } })

    renderApp('/')

    await screen.findByRole('grid')
    expect(screen.getAllByRole('columnheader').map((th) => th.textContent.trim())).toEqual([
      '', 'Name', 'Description', 'Brand', 'Type', 'Price', 'Picture name', 'Stock', 'Restock', 'Max stock', '',
    ])
  })

  it('links each row to its pages', async () => {
    stubFetch({ [items]: { status: 200, body: page({ Data: [catalogItem({ Id: 7 })] }) } })

    renderApp('/')

    expect(await screen.findByRole('link', { name: 'Edit' })).toHaveAttribute('href', '/Catalog/Edit/7')
    expect(screen.getByRole('link', { name: 'Details' })).toHaveAttribute('href', '/Catalog/Details/7')
    expect(screen.getByRole('link', { name: 'Delete' })).toHaveAttribute('href', '/Catalog/Delete/7')
    expect(screen.getByRole('link', { name: 'Create New' })).toHaveAttribute('href', '/Catalog/Create')
  })

  it('opens the details page from the list without leaving the app', async () => {
    stubFetch({
      [items]: { status: 200, body: page({ Data: [catalogItem({ Id: 7 })] }) },
      [`${items}/7`]: { status: 200, body: catalogItem({ Id: 7 }) },
    })

    renderApp('/')
    await userEvent.click(await screen.findByRole('link', { name: 'Details' }))

    expect(await screen.findByRole('heading', { level: 2 })).toHaveTextContent('Details')
    expect(await screen.findByText('.NET Bot Black Hoodie')).toBeInTheDocument()
  })

  it('fills the window on the list page only (P3-4)', async () => {
    stubFetch({
      [items]: { status: 200, body: page({ Data: [catalogItem({ Id: 7 })] }) },
      [`${items}/7`]: { status: 200, body: catalogItem({ Id: 7 }) },
    })

    renderApp('/')
    expect(document.body).toHaveClass('esh-fill')
    await userEvent.click(await screen.findByRole('link', { name: 'Details' }))

    expect(await screen.findByRole('heading', { level: 2 })).toHaveTextContent('Details')
    expect(document.body).not.toHaveClass('esh-fill')
  })

  it('formats prices as en-US currency with thousands separators', async () => {
    stubFetch({ [items]: { status: 200, body: page({ Data: [catalogItem({ Price: 1234.5 })] }) } })

    renderApp('/')

    expect(await screen.findByText('$1,234.50')).toBeInTheDocument()
  })

  it('passes the paging values of the URL to the API and follows the pager', async () => {
    const second = page({ ActualPage: 1, ItemsPerPage: 3, Data: [catalogItem({ Id: 4, Name: 'Second page item' })] })
    const third = page({ ActualPage: 2, ItemsPerPage: 3, Data: [catalogItem({ Id: 7, Name: 'Third page item' })] })
    const fetchMock = stubFetch({
      [`${items}?pageSize=3&pageIndex=1`]: { status: 200, body: { ...second, TotalPages: 4 } },
      [`${items}?pageSize=3&pageIndex=2`]: { status: 200, body: { ...third, TotalPages: 4 } },
    })

    renderApp('/Catalog/Index?pageSize=3&pageIndex=1')
    await screen.findByText('Second page item')
    await userEvent.click(screen.getByRole('link', { name: 'Next' }))

    expect(await screen.findByText('Third page item')).toBeInTheDocument()
    expect(screen.queryByText('Second page item')).not.toBeInTheDocument()
    expect(screen.getByText('Showing 1 of 12 products - Page 3 - 4')).toBeInTheDocument()
    expect(requestedUrls(fetchMock)).toContain(`${items}?pageSize=3&pageIndex=2`)
  })

  it('shows the last page when the URL asks for a page beyond it (P3-9)', async () => {
    const lastPage = [catalogItem({ Id: 11, Name: 'Eleventh item' }), catalogItem({ Id: 12, Name: 'Twelfth item' })]
    const fetchMock = stubFetch({
      [`${items}?pageSize=10&pageIndex=3`]: { status: 200, body: page({ ActualPage: 3, Data: [] }) },
      [`${items}?pageSize=10&pageIndex=1`]: { status: 200, body: page({ ActualPage: 1, Data: lastPage }) },
    })

    renderApp('/?pageSize=10&pageIndex=3')

    expect(await screen.findByText('Twelfth item')).toBeInTheDocument()
    expect(screen.getByText('Showing 2 of 12 products - Page 2 - 2')).toBeInTheDocument()
    expect(screen.queryByText(/Page 4/)).not.toBeInTheDocument()
    expect(requestedUrls(fetchMock)).toEqual([`${items}?pageSize=10&pageIndex=3`, `${items}?pageSize=10&pageIndex=1`])
  })

  it('shows the first page of an empty catalog whatever page the URL asks for (P3-9)', async () => {
    const empty = { TotalItems: 0, TotalPages: 0, Data: [] }
    stubFetch({
      [`${items}?pageSize=10&pageIndex=3`]: { status: 200, body: page({ ...empty, ActualPage: 3 }) },
      [`${items}?pageSize=10&pageIndex=0`]: { status: 200, body: page({ ...empty, ActualPage: 0 }) },
    })

    renderApp('/?pageSize=10&pageIndex=3')

    expect(await screen.findByText('Showing 0 of 0 products - Page 1 - 0')).toBeInTheDocument()
  })

  it('says so when the server rejects the paging values (R-16)', async () => {
    stubFetch({ [`${items}?pageSize=0`]: { status: 400 } })

    renderApp('/?pageSize=0')

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid paging values.')
    expect(screen.queryByRole('grid')).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Create New' })).toBeInTheDocument()
  })

  it('says so when the catalog cannot be loaded', async () => {
    stubFetch({ [items]: { status: 500 } })

    renderApp('/')

    expect(await screen.findByRole('alert')).toHaveTextContent('The catalog could not be loaded.')
  })

  it('has no footer and asks the server for nothing but the list (P3-7)', async () => {
    const fetchMock = stubFetch({ [items]: { status: 200, body: page() } })

    renderApp('/')

    await screen.findByRole('grid')
    expect(document.querySelector('footer')).toBeNull()
    expect(requestedUrls(fetchMock)).toEqual([items])
  })

  it('shows a message for an unknown route', () => {
    stubFetch({})

    renderApp('/Nope')

    expect(screen.getByRole('alert')).toHaveTextContent('Page not found.')
  })
})
