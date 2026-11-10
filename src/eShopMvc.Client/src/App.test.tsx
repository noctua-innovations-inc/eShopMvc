import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import App from './App.tsx'
import { catalogItem, page, requestedUrls, session, stubFetch } from './test/fakeApi.ts'

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
    stubFetch({ [items]: { status: 200, body: page() }, '/api/v1/session': { status: 200, body: session } })

    renderApp(url)

    const row = (await screen.findByText('.NET Bot Black Hoodie')).closest('tr')!
    expect(within(row).getByText('$19.50')).toHaveClass('esh-table-price')
    expect(within(row).getByText('.NET')).toBeInTheDocument()
    expect(within(row).getByText('T-Shirt')).toBeInTheDocument()
    expect(within(row).getByRole('img')).toHaveAttribute('src', 'http://localhost/items/1/pic')
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Catalog manager (MVC)')
    expect(document.title).toBe('Index - Catalog manager (MVC)')
    expect(await screen.findByText('TESTBOX, 10/29/2026 9:05:07 AM')).toBeInTheDocument()
  })

  it('uses the Razor column labels', async () => {
    stubFetch({ [items]: { status: 200, body: page() } })

    renderApp('/')

    await screen.findByRole('table')
    expect(screen.getAllByRole('columnheader').map((th) => th.textContent)).toEqual([
      '', 'Name', 'Description', 'Brand', 'Type', 'Price', 'Picture name', 'Stock', 'Restock', 'Max stock', '',
    ])
  })

  it('links to the Razor pages until the React ones exist', async () => {
    stubFetch({ [items]: { status: 200, body: page({ Data: [catalogItem({ Id: 7 })] }) } })

    renderApp('/')

    expect(await screen.findByRole('link', { name: 'Edit' })).toHaveAttribute('href', '/Catalog/Edit/7')
    expect(screen.getByRole('link', { name: 'Details' })).toHaveAttribute('href', '/Catalog/Details/7')
    expect(screen.getByRole('link', { name: 'Delete' })).toHaveAttribute('href', '/Catalog/Delete/7')
    expect(screen.getByRole('link', { name: 'Create New' })).toHaveAttribute('href', '/Catalog/Create')
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
    expect(screen.getByText('Showing 3 of 12 products - Page 3 - 4')).toBeInTheDocument()
    expect(requestedUrls(fetchMock)).toContain(`${items}?pageSize=3&pageIndex=2`)
  })

  it('says so when the server rejects the paging values (R-16)', async () => {
    stubFetch({ [`${items}?pageSize=0`]: { status: 400 } })

    renderApp('/?pageSize=0')

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid paging values.')
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Create New' })).toBeInTheDocument()
  })

  it('says so when the catalog cannot be loaded', async () => {
    stubFetch({ [items]: { status: 500 } })

    renderApp('/')

    expect(await screen.findByRole('alert')).toHaveTextContent('The catalog could not be loaded.')
  })

  it('leaves the footer text empty when the session call fails', async () => {
    stubFetch({ [items]: { status: 200, body: page() } })

    renderApp('/')

    await screen.findByRole('table')
    expect(document.querySelector('footer small')).toBeEmptyDOMElement()
  })

  it('shows a message for an unknown route', () => {
    stubFetch({})

    renderApp('/Nope')

    expect(screen.getByRole('alert')).toHaveTextContent('Page not found.')
  })
})
