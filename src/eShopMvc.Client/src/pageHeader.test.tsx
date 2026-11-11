import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { BrowserRouter } from 'react-router'
import { afterEach, describe, expect, it } from 'vitest'
import App from './App.tsx'
import { catalogItem, listRoutes, page, session, stubFetch } from './test/fakeApi.ts'

// Decision P2-19. With the BrowserRouter of main.tsx, because the header is the browser's own URL.
describe('X-Client-Page', () => {
  afterEach(() => {
    window.history.replaceState(null, '', '/')
    document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT'
  })

  function pagesByUrl(fetchMock: ReturnType<typeof stubFetch>): Record<string, string | undefined> {
    return Object.fromEntries(
      fetchMock.mock.calls.map(([input, init]) => [
        `${init?.method ?? 'GET'} ${String(input)}`,
        (init?.headers as Record<string, string> | undefined)?.['X-Client-Page'],
      ]),
    )
  }

  it('names the page of every call, follows a navigation inside the app and leaves the query string out', async () => {
    const user = userEvent.setup()
    window.history.replaceState(null, '', '/Catalog/Index?pageSize=5&pageIndex=0')
    document.cookie = 'XSRF-TOKEN=abc'
    const fetchMock = stubFetch({
      '/api/v1/catalog/items?pageSize=5&pageIndex=0': { status: 200, body: page({ Data: [catalogItem({ Id: 7 })] }) },
      '/api/v1/catalog/items': { status: 200, body: page() },
      '/api/v1/catalog/items/7':{ status: 200, body: catalogItem({ Id: 7 }) },
      '/api/v1/session': { status: 200, body: session },
      'PUT /api/v1/catalog/items/7': { status: 204 },
      ...listRoutes,
    })

    render(
      <BrowserRouter>
        <App />
      </BrowserRouter>,
    )
    await user.click(await screen.findByRole('link', { name: 'Edit' }))
    await user.click(await screen.findByRole('button', { name: '[ Save ]' }))
    await screen.findByRole('table')

    expect(pagesByUrl(fetchMock)).toEqual({
      'GET /api/v1/catalog/items?pageSize=5&pageIndex=0': '/Catalog/Index',
      'GET /api/v1/session': '/Catalog/Index',
      'GET /api/v1/catalog/items/7': '/Catalog/Edit/7',
      'GET /api/v1/catalog/brands': '/Catalog/Edit/7',
      'GET /api/v1/catalog/types': '/Catalog/Edit/7',
      'PUT /api/v1/catalog/items/7': '/Catalog/Edit/7',
      'GET /api/v1/catalog/items': '/',
    })
  })
})
