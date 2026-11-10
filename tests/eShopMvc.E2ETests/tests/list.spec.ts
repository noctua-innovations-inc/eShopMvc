import { app, getPage } from '../support/app.ts'
import { expect, test } from '../support/fixtures.ts'

const hidden = /esh-pager-item--hidden/

test.describe('Catalog list', () => {
  for (const path of ['/', '/Catalog', '/Catalog/Index']) {
    test(`shows the first page at ${path}`, async ({ page, request }) => {
      const first = await getPage(request)

      await page.goto(app(path))

      await expect(page.getByRole('heading', { level: 1 })).toHaveText('Catalog manager (MVC)')
      await expect(page).toHaveTitle('Index - Catalog manager (MVC)')
      await expect(page.locator('tbody tr')).toHaveCount(first.Data.length)
      await expect(page.locator('thead th')).toHaveText([
        '', 'Name', 'Description', 'Brand', 'Type', 'Price', 'Picture name', 'Stock', 'Restock', 'Max stock', '',
      ])
      await expect(page.locator('span.esh-pager-item')).toHaveText(
        `Showing 10 of ${first.TotalItems} products - Page 1 - ${first.TotalPages}`,
      )
      await expect(page.getByRole('link', { name: 'Create New' })).toBeVisible()
    })
  }

  test('shows each item with its brand, type, price and picture', async ({ page, request }) => {
    const item = (await getPage(request)).Data[0]

    await page.goto(app('/'))

    const row = page.locator('tbody tr').first()
    await expect(row.locator('td').nth(1)).toHaveText(item.Name)
    await expect(row.locator('td').nth(3)).toHaveText(item.CatalogBrand.Brand)
    await expect(row.locator('td').nth(4)).toHaveText(item.CatalogType.Type)
    await expect(row.locator('td.esh-table-price')).toHaveText(`$${item.Price.toFixed(2)}`)
    await expect(row.locator('td').nth(10)).toHaveText('Edit | Details | Delete')
    await expect(row.locator('img.esh-thumbnail')).toHaveJSProperty('complete', true)
    expect(await row.locator('img.esh-thumbnail').evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(0)
  })

  test('pages forward and back with Next and Previous', async ({ page, request }) => {
    const pageSize = 5
    const first = await getPage(request, pageSize, 0)
    const second = await getPage(request, pageSize, 1)
    test.skip(first.TotalPages < 3, 'needs at least three pages of five (the seed has twelve items)')
    const previous = page.locator('a.esh-pager-item', { hasText: 'Previous' })
    const next = page.locator('a.esh-pager-item', { hasText: 'Next' })

    await page.goto(app(`/?pageSize=${pageSize}&pageIndex=0`))
    await expect(page.locator('tbody tr')).toHaveCount(pageSize)
    await expect(previous).toHaveClass(hidden)
    await expect(previous).toHaveCSS('opacity', '0') // how site.css hides it; it keeps its place
    await expect(next).not.toHaveClass(hidden)

    await next.click()
    await expect(page).toHaveURL(/\?pageSize=5&pageIndex=1$/)
    await expect(page.locator('span.esh-pager-item')).toHaveText(
      `Showing 5 of ${first.TotalItems} products - Page 2 - ${first.TotalPages}`,
    )
    await expect(page.locator('tbody tr').first().locator('td').nth(1)).toHaveText(second.Data[0].Name)
    await expect(previous).not.toHaveClass(hidden)

    await previous.click()
    await expect(page).toHaveURL(/pageIndex=0$/)
    await expect(page.locator('tbody tr').first().locator('td').nth(1)).toHaveText(first.Data[0].Name)

    await page.goBack()
    await expect(page.locator('span.esh-pager-item')).toContainText('Page 2')
  })

  test('hides Next on the last page', async ({ page, request }) => {
    const pageSize = 5
    const first = await getPage(request, pageSize, 0)
    const lastIndex = first.TotalPages - 1
    const last = await getPage(request, pageSize, lastIndex)

    await page.goto(app(`/?pageSize=${pageSize}&pageIndex=${lastIndex}`))

    await expect(page.locator('tbody tr')).toHaveCount(last.Data.length)
    // "Showing 5" on a shorter last page is the Razor UI's text (R-24).
    await expect(page.locator('span.esh-pager-item')).toHaveText(
      `Showing 5 of ${first.TotalItems} products - Page ${lastIndex + 1} - ${first.TotalPages}`,
    )
    await expect(page.locator('a.esh-pager-item', { hasText: 'Next' })).toHaveClass(hidden)
    await expect(page.locator('a.esh-pager-item', { hasText: 'Previous' })).not.toHaveClass(hidden)
  })

  test('says so for invalid paging values (P2-14)', async ({ page }) => {
    await page.goto(app('/?pageSize=0'))

    await expect(page.getByRole('alert')).toHaveText('Invalid paging values.')
    await expect(page.locator('table')).toHaveCount(0)
  })
})
