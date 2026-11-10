import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import App from '../App.tsx'
import { catalogItem, listRoutes, page, sentRequest, stubFetch } from '../test/fakeApi.ts'

const items = '/api/v1/catalog/items'
const item5 = `${items}/5`
const stored = catalogItem({ Id: 5, Name: 'Stored mug', Description: 'A mug', Price: 19.5, AvailableStock: 100 })
const list = { [items]: { status: 200, body: page({ Data: [catalogItem({ Id: 9, Name: 'Listed item' })] }) } }

function renderApp(url: string) {
  render(
    <MemoryRouter initialEntries={[url]}>
      <App />
    </MemoryRouter>,
  )
}

function fieldError(field: string): string {
  return document.querySelector(`#${field}`)!.parentElement!.querySelector('span.text-danger')!.textContent
}

function validationProblem(errors: Record<string, string[]>) {
  return { status: 400, body: { title: 'One or more validation errors occurred.', status: 400, errors } }
}

beforeEach(() => {
  document.cookie = 'XSRF-TOKEN=abc%2Bdef%3D; path=/'
})

afterEach(() => {
  document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/'
})

describe('Create page', () => {
  it('starts with the Razor form: labels, start values, drop-downs and the upload note', async () => {
    stubFetch(listRoutes)

    renderApp('/Catalog/Create')

    expect(await screen.findByLabelText('Name')).toHaveValue('')
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('Create')
    expect(document.title).toBe('Create - Catalog manager (MVC)')
    expect([...document.querySelectorAll('label')].map((label) => label.textContent)).toEqual([
      'Name', 'Description', 'Brand', 'Type', 'Price', 'Picture name', 'Stock', 'Restock', 'Max stock',
    ])
    expect(screen.getByLabelText('Price')).toHaveValue('0.00')
    expect(screen.getByLabelText('Stock')).toHaveValue(0)
    expect(screen.getByLabelText('Brand')).toHaveValue('1')
    expect([...screen.getByLabelText<HTMLSelectElement>('Type').options].map((o) => o.text)).toEqual(['Mug', 'T-Shirt'])
    expect(screen.getByText(/Uploading images not allowed for this version\./)).toBeInTheDocument()
    expect(document.querySelector('#PictureFileName')).toBeNull()
    expect(screen.getByRole('link', { name: '[ Cancel ]' })).toHaveAttribute('href', '/')
  })

  it('posts the entered values with the antiforgery token and returns to the list', async () => {
    const fetchMock = stubFetch({ ...listRoutes, ...list, [`POST ${items}`]: { status: 201, body: stored } })
    const user = userEvent.setup()

    renderApp('/Catalog/Create')
    await user.type(await screen.findByLabelText('Name'), 'New mug')
    await user.selectOptions(screen.getByLabelText('Brand'), '.NET')
    await user.clear(screen.getByLabelText('Price'))
    await user.type(screen.getByLabelText('Price'), '12.50')
    await user.clear(screen.getByLabelText('Stock'))
    await user.type(screen.getByLabelText('Stock'), '7')
    await user.click(screen.getByRole('button', { name: '[ Create ]' }))

    expect(await screen.findByText('Listed item')).toBeInTheDocument()
    const request = sentRequest(fetchMock, 'POST')
    expect(request.headers).toMatchObject({ 'X-XSRF-TOKEN': 'abc+def=', 'Content-Type': 'application/json' })
    expect(request.body).toEqual({
      Name: 'New mug',
      Description: null,
      Price: 12.5,
      PictureFileName: 'dummy.png',
      CatalogTypeId: 1, // the first option, never touched
      CatalogBrandId: 2,
      AvailableStock: 7,
      RestockThreshold: 0,
      MaxStockThreshold: 0,
    })
  })

  it('checks the name and the numbers before sending anything (P2-16)', async () => {
    const fetchMock = stubFetch(listRoutes)
    const user = userEvent.setup()

    renderApp('/Catalog/Create')
    await user.clear(await screen.findByLabelText('Price'))
    await user.type(screen.getByLabelText('Price'), 'abc')
    await user.clear(screen.getByLabelText('Restock'))
    await user.click(screen.getByRole('button', { name: '[ Create ]' }))

    expect(fieldError('Name')).toBe('The Name field is required.')
    expect(fieldError('Price')).toBe('The field Price must be a number.')
    expect(fieldError('RestockThreshold')).toBe('The Restock field is required.')
    expect(fieldError('AvailableStock')).toBe('')
    expect(screen.getByLabelText('Price')).toHaveClass('input-validation-error')
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST')).toEqual([])
  })

  it("shows the server's messages at their fields and keeps the entered values", async () => {
    stubFetch({
      ...listRoutes,
      [`POST ${items}`]: validationProblem({
        Price: ['The field Price must be a positive number with maximum two decimals.'],
        AvailableStock: ['The field Stock must be between 0 and 10 million.'],
        CatalogBrandId: ['The selected brand does not exist.'],
        PictureFileName: ["The field Picture name must be a file name made of letters, digits, '-', '_' and '.'."],
      }),
    })
    const user = userEvent.setup()

    renderApp('/Catalog/Create')
    await user.type(await screen.findByLabelText('Name'), 'New mug')
    await user.clear(screen.getByLabelText('Price'))
    await user.type(screen.getByLabelText('Price'), '1.999')
    await user.click(screen.getByRole('button', { name: '[ Create ]' }))

    expect(await screen.findByText('The field Stock must be between 0 and 10 million.')).toBeInTheDocument()
    expect(fieldError('Price')).toBe('The field Price must be a positive number with maximum two decimals.')
    expect(fieldError('CatalogBrandId')).toBe('The selected brand does not exist.')
    expect(screen.getByText(/The field Picture name must be a file name/)).toHaveClass('field-validation-error')
    expect(fieldError('Name')).toBe('')
    expect(screen.getByLabelText('Name')).toHaveValue('New mug')
    expect(screen.getByLabelText('Price')).toHaveValue('1.999')
    expect(screen.getByRole('button', { name: '[ Create ]' })).toBeEnabled()
  })

  it.each([
    ['a rejected antiforgery token', { status: 400 }],
    ['a server error', { status: 500 }],
  ])('stays on the form and says so after %s', async (_, reply) => {
    stubFetch({ ...listRoutes, [`POST ${items}`]: reply })
    const user = userEvent.setup()

    renderApp('/Catalog/Create')
    await user.type(await screen.findByLabelText('Name'), 'New mug')
    await user.click(screen.getByRole('button', { name: '[ Create ]' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('The catalog item could not be saved.')
    expect(screen.getByLabelText('Name')).toHaveValue('New mug')
  })

  it('says so when the drop-down lists cannot be loaded', async () => {
    stubFetch({ '/api/v1/catalog/brands': { status: 500 }, '/api/v1/catalog/types': listRoutes['/api/v1/catalog/types'] })

    renderApp('/Catalog/Create')

    expect(await screen.findByRole('alert')).toHaveTextContent('The catalog could not be loaded.')
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })
})

describe('Edit page', () => {
  it("starts with the item's values, the picture and the Razor labels (R-24)", async () => {
    stubFetch({ ...listRoutes, [item5]: { status: 200, body: stored } })

    renderApp('/Catalog/Edit/5')

    expect(await screen.findByLabelText('Name')).toHaveValue('Stored mug')
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('Edit')
    expect(document.title).toBe('Edit - Catalog manager (MVC)')
    expect([...document.querySelectorAll('label')].map((label) => label.textContent)).toEqual([
      'Name', 'Description', 'CatalogBrandId', 'CatalogTypeId', 'Price', 'Picture name', 'Stock', 'Restock', 'Max stock',
    ])
    expect(screen.getByLabelText('Description')).toHaveValue('A mug')
    expect(screen.getByLabelText('CatalogBrandId')).toHaveValue('2')
    expect(screen.getByLabelText('CatalogTypeId')).toHaveValue('2')
    expect(screen.getByLabelText('Price')).toHaveValue('19.50')
    expect(screen.getByLabelText('Stock')).toHaveValue(100)
    expect(screen.getByLabelText('Picture name')).toHaveValue('1.png')
    expect(screen.getByLabelText('Picture name')).toHaveAttribute('readonly')
    expect(screen.getByLabelText('Picture name')).toHaveAttribute('title', 'Not allowed for edition')
    expect(document.querySelector('img.esh-picture')).toHaveAttribute('src', 'http://localhost/items/1/pic')
  })

  it('puts the changed values with the id and the antiforgery token and returns to the list', async () => {
    const fetchMock = stubFetch({ ...listRoutes, ...list, [item5]: { status: 200, body: stored }, [`PUT ${item5}`]: { status: 204 } })
    const user = userEvent.setup()

    renderApp('/Catalog/Edit/5')
    await user.clear(await screen.findByLabelText('Name'))
    await user.type(screen.getByLabelText('Name'), 'Renamed mug')
    await user.clear(screen.getByLabelText('Description'))
    await user.selectOptions(screen.getByLabelText('CatalogTypeId'), 'Mug')
    await user.click(screen.getByRole('button', { name: '[ Save ]' }))

    expect(await screen.findByText('Listed item')).toBeInTheDocument()
    const request = sentRequest(fetchMock, 'PUT')
    expect(request.url).toBe(item5)
    expect(request.headers).toMatchObject({ 'X-XSRF-TOKEN': 'abc+def=' })
    expect(request.body).toEqual({
      Id: 5,
      Name: 'Renamed mug',
      Description: null,
      Price: 19.5,
      PictureFileName: '1.png',
      CatalogTypeId: 1,
      CatalogBrandId: 2,
      AvailableStock: 100,
      RestockThreshold: 0,
      MaxStockThreshold: 0,
      OnReorder: false,
    })
  })

  it('sends the stored OnReorder back, so an edit does not reset it (P2-17)', async () => {
    const onReorder = { ...stored, OnReorder: true }
    const fetchMock = stubFetch({ ...listRoutes, ...list, [item5]: { status: 200, body: onReorder }, [`PUT ${item5}`]: { status: 204 } })

    renderApp('/Catalog/Edit/5')
    await userEvent.click(await screen.findByRole('button', { name: '[ Save ]' }))

    await screen.findByText('Listed item')
    expect(sentRequest(fetchMock, 'PUT').body).toMatchObject({ Id: 5, OnReorder: true })
  })

  it('sends no OnReorder for a new item', async () => {
    const fetchMock = stubFetch({ ...listRoutes, ...list, [`POST ${items}`]: { status: 201, body: stored } })
    const user = userEvent.setup()

    renderApp('/Catalog/Create')
    await user.type(await screen.findByLabelText('Name'), 'New mug')
    await user.click(screen.getByRole('button', { name: '[ Create ]' }))

    await screen.findByText('Listed item')
    expect(sentRequest(fetchMock, 'POST').body).not.toHaveProperty('OnReorder')
  })

  it("shows the server's messages at their fields", async () => {
    stubFetch({
      ...listRoutes,
      [item5]: { status: 200, body: stored },
      [`PUT ${item5}`]: validationProblem({ Price: ['The field Price must be between 0 and 1000000.'] }),
    })
    const user = userEvent.setup()

    renderApp('/Catalog/Edit/5')
    await user.click(await screen.findByRole('button', { name: '[ Save ]' }))

    expect(await screen.findByText('The field Price must be between 0 and 1000000.')).toBeInTheDocument()
    expect(fieldError('Price')).toBe('The field Price must be between 0 and 1000000.')
  })

  it('says so when the item was deleted in the meantime', async () => {
    stubFetch({ ...listRoutes, [item5]: { status: 200, body: stored } }) // PUT: 404

    renderApp('/Catalog/Edit/5')
    await userEvent.click(await screen.findByRole('button', { name: '[ Save ]' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('The catalog item was not found.')
    expect(screen.getByLabelText('Name')).toHaveValue('Stored mug')
  })

  it('shows a message instead of the form for an item that does not exist (P2-15)', async () => {
    stubFetch(listRoutes)

    renderApp('/Catalog/Edit/999')

    expect(await screen.findByRole('alert')).toHaveTextContent('The catalog item was not found.')
    expect(screen.getByRole('link', { name: 'Back to List' })).toHaveAttribute('href', '/')
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('says so when the item cannot be loaded', async () => {
    stubFetch({ ...listRoutes, [item5]: { status: 500 } })

    renderApp('/Catalog/Edit/5')

    expect(await screen.findByRole('alert')).toHaveTextContent('The catalog item could not be loaded.')
  })
})

describe('links to the forms', () => {
  it('opens Create and Edit from the list without leaving the app', async () => {
    stubFetch({ ...listRoutes, ...list, [`${items}/9`]: { status: 200, body: catalogItem({ Id: 9, Name: 'Listed item' }) } })
    const user = userEvent.setup()

    renderApp('/')
    await user.click(await screen.findByRole('link', { name: 'Edit' }))
    expect(await screen.findByLabelText('Name')).toHaveValue('Listed item')

    await user.click(screen.getByRole('link', { name: '[ Cancel ]' }))
    await user.click(await screen.findByRole('link', { name: 'Create New' }))
    expect(await screen.findByRole('button', { name: '[ Create ]' })).toBeInTheDocument()
  })
})
