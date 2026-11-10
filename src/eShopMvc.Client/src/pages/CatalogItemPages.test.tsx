import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it } from 'vitest'
import App from '../App.tsx'
import { catalogItem, page, requestedUrls, stubFetch } from '../test/fakeApi.ts'

const items = '/api/v1/catalog/items'
const item5 = `${items}/5`

function renderApp(url: string) {
  render(
    <MemoryRouter initialEntries={[url]}>
      <App />
    </MemoryRouter>,
  )
}

function terms(): [string, string][] {
  const dts = [...document.querySelectorAll('dl.dl-horizontal > dt')]
  return dts.map((dt) => [dt.textContent, dt.nextElementSibling?.textContent ?? ''])
}

afterEach(() => {
  document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/'
})

describe('Details page', () => {
  it('shows the item with the Razor labels, picture and links', async () => {
    stubFetch({ [item5]: { status: 200, body: catalogItem({ Id: 5, Price: 1234.5, AvailableStock: 100 }) } })

    renderApp('/Catalog/Details/5')

    expect(await screen.findByText('.NET Bot Black Hoodie')).toBeInTheDocument()
    expect(terms()).toEqual([
      ['Name', '.NET Bot Black Hoodie'],
      ['Description', '.NET Bot Black Hoodie, and more'],
      ['Brand', '.NET'],
      ['Type', 'T-Shirt'],
      ['Price', '$1,234.50'],
      ['Picture name', '1.png'],
      ['Stock', '100'],
      ['Restock', '0'],
      ['Max stock', '0'],
    ])
    expect(document.querySelector('img.esh-picture')).toHaveAttribute('src', 'http://localhost/items/1/pic')
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('Details')
    expect(document.title).toBe('Details - Catalog manager (MVC)')
    expect(screen.getByRole('link', { name: 'Edit' })).toHaveAttribute('href', '/Catalog/Edit/5')
    expect(screen.getByRole('link', { name: 'Back to List' })).toHaveAttribute('href', '/')
  })

  it('says so when the item does not exist', async () => {
    stubFetch({})

    renderApp('/Catalog/Details/999')

    expect(await screen.findByRole('alert')).toHaveTextContent('The catalog item was not found.')
    expect(screen.getByRole('link', { name: 'Back to List' })).toHaveAttribute('href', '/')
    expect(screen.queryByRole('link', { name: 'Edit' })).not.toBeInTheDocument()
  })

  it.each([
    ['abc', 'abc'],
    ['-1', '-1'],
    ['1.5', '1.5'],
    ['5%20x', '5%20x'],
  ])('lets the server decide about the id "%s": its 404 is "not found"', async (id, sent) => {
    const fetchMock = stubFetch({})

    renderApp(`/Catalog/Details/${id}`)

    expect(await screen.findByRole('alert')).toHaveTextContent('The catalog item was not found.')
    expect(requestedUrls(fetchMock)).toContain(`${items}/${sent}`)
  })

  it('shows the item the server finds for an id it accepts in another spelling', async () => {
    stubFetch({ [`${items}/05`]: { status: 200, body: catalogItem({ Id: 5 }) } })

    renderApp('/Catalog/Details/05')

    expect(await screen.findByText('.NET Bot Black Hoodie')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Edit' })).toHaveAttribute('href', '/Catalog/Edit/5')
  })

  it('says so when the item cannot be loaded', async () => {
    stubFetch({ [item5]: { status: 500 } })

    renderApp('/Catalog/Details/5')

    expect(await screen.findByRole('alert')).toHaveTextContent('The catalog item could not be loaded.')
  })
})

describe('Delete page', () => {
  it('asks for confirmation and shows the item', async () => {
    stubFetch({ [item5]: { status: 200, body: catalogItem({ Id: 5 }) } })

    renderApp('/Catalog/Delete/5')

    expect(await screen.findByRole('heading', { level: 3 })).toHaveTextContent('Are you sure you want to delete this?')
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('Delete')
    expect(document.title).toBe('Delete - Catalog manager (MVC)')
    expect(terms().slice(0, 1)).toEqual([['Name', '.NET Bot Black Hoodie']])
    expect(screen.getByRole('link', { name: '[ Cancel ]' })).toHaveAttribute('href', '/')
    expect(screen.getByRole('button', { name: '[ Delete ]' })).toBeEnabled()
  })

  it('deletes with the decoded antiforgery token and returns to the list', async () => {
    document.cookie = 'XSRF-TOKEN=abc%2Bdef%3D; path=/'
    const fetchMock = stubFetch({
      [item5]: { status: 200, body: catalogItem({ Id: 5 }) },
      [`DELETE ${item5}`]: { status: 204 },
      [items]: { status: 200, body: page({ Data: [catalogItem({ Id: 6, Name: 'Remaining item' })] }) },
    })

    renderApp('/Catalog/Delete/5')
    await userEvent.click(await screen.findByRole('button', { name: '[ Delete ]' }))

    expect(await screen.findByText('Remaining item')).toBeInTheDocument()
    const [, init] = fetchMock.mock.calls.find(([, i]) => i?.method === 'DELETE')!
    expect(init?.headers).toMatchObject({ 'X-XSRF-TOKEN': 'abc+def=' })
  })

  it('stays on the page and says so when the item is already gone (R-15)', async () => {
    stubFetch({ [item5]: { status: 200, body: catalogItem({ Id: 5 }) }, [`DELETE ${item5}`]: { status: 404 } })

    renderApp('/Catalog/Delete/5')
    await userEvent.click(await screen.findByRole('button', { name: '[ Delete ]' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('The catalog item was not found.')
    expect(screen.getByRole('button', { name: '[ Delete ]' })).toBeEnabled()
  })

  it('stays on the page and says so when the delete is rejected (no token: 400)', async () => {
    const fetchMock = stubFetch({
      [item5]: { status: 200, body: catalogItem({ Id: 5 }) },
      [`DELETE ${item5}`]: { status: 400 },
    })

    renderApp('/Catalog/Delete/5')
    await userEvent.click(await screen.findByRole('button', { name: '[ Delete ]' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('The catalog item could not be deleted.')
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('Delete')
    const [, init] = fetchMock.mock.calls.find(([, i]) => i?.method === 'DELETE')!
    expect(init?.headers).not.toHaveProperty('X-XSRF-TOKEN')
  })

  it('shows no delete button for an item that does not exist', async () => {
    stubFetch({})

    renderApp('/Catalog/Delete/999')

    expect(await screen.findByRole('alert')).toHaveTextContent('The catalog item was not found.')
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })
})
