import { app, appUrl, getPage } from '../support/app.ts'
import { expect, test } from '../support/fixtures.ts'

const hidden = /esh-pager-item--hidden/

// Decision P3-5: the list is an AG Grid. Its rows are divs with the class ag-row, its cells and
// headers divs with the roles gridcell and columnheader.

test.describe('Catalog list', () => {
  for (const path of ['/', '/Catalog', '/Catalog/Index']) {
    test(`shows the first page at ${path}`, async ({ page, request }) => {
      const first = await getPage(request)

      await page.goto(app(path))

      await expect(page.getByRole('heading', { level: 1 })).toHaveText('Catalog manager (MVC)')
      await expect(page).toHaveTitle('Index - Catalog manager (MVC)')
      await expect(page.locator('.ag-row')).toHaveCount(first.Data.length)
      await expect(page.locator('[role="columnheader"]')).toHaveText([
        '', 'Name', 'Description', 'Brand', 'Type', 'Price', 'Picture name', 'Stock', 'Restock', 'Max stock', '',
      ])
      await expect(page.locator('span.esh-pager-item')).toHaveText(
        `Showing ${first.Data.length} of ${first.TotalItems} products - Page 1 - ${first.TotalPages}`,
      )
      await expect(page.getByRole('link', { name: 'Create New' })).toBeVisible()
    })
  }

  test('uses the smaller sizes of P3-8', async ({ page }) => {
    await page.goto(app('/'))
    const row = page.locator('.ag-row').first()
    const thumbnail = row.locator('img.esh-thumbnail')
    await expect(thumbnail).toHaveJSProperty('complete', true)

    await expect(row.locator('[role="gridcell"]').nth(1)).toHaveCSS('font-size', '14px')
    await expect(row.locator('[role="gridcell"]').nth(1)).toHaveCSS('padding-top', '6px')
    await expect(page.getByRole('link', { name: 'Create New' })).toHaveCSS('font-size', '14px')
    await expect(page.getByRole('link', { name: 'Create New' })).toHaveCSS('padding-top', '7px')
    await expect(page.locator('.esh-app-hero')).toHaveCSS('height', '72px')
    await expect(page.getByRole('heading', { level: 1 })).toHaveCSS('font-size', '28px')
    expect(await thumbnail.evaluate((img) => Math.round(img.getBoundingClientRect().width))).toBe(84)
    // No column label is cut off.
    for (const label of await page.locator('.ag-header-cell-text').all()) {
      expect(await label.evaluate((text) => text.scrollWidth <= text.clientWidth), await label.textContent() ?? '').toBe(true)
    }
  })

  test('shows each item with its brand, type, price and picture', async ({ page, request }) => {
    const item = (await getPage(request)).Data[0]

    await page.goto(app('/'))

    const row = page.locator('.ag-row').first()
    await expect(row.locator('[role="gridcell"]').nth(1)).toHaveText(item.Name)
    await expect(row.locator('[role="gridcell"]').nth(3)).toHaveText(item.CatalogBrand.Brand)
    await expect(row.locator('[role="gridcell"]').nth(4)).toHaveText(item.CatalogType.Type)
    await expect(row.locator('.esh-table-price')).toHaveText(`$${item.Price.toFixed(2)}`)
    await expect(row.locator('[role="gridcell"]').nth(10)).toHaveText('Edit | Details | Delete')
    await expect(row.locator('img.esh-thumbnail')).toHaveJSProperty('complete', true)
    expect(await row.locator('img.esh-thumbnail').evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(0)
  })

  test('reaches the links of the rows with the Tab key and leaves the grid after the last row (P3-5)', async ({ page, request }) => {
    const items = (await getPage(request)).Data
    const focused = page.locator(':focus')

    await page.goto(app('/'))
    await expect(page.locator('.ag-row a').first()).toBeVisible()
    await page.getByRole('link', { name: 'Create New' }).focus()

    await page.keyboard.press('Tab') // the links' cell of the first row
    await expect(focused).toHaveText('Edit | Details | Delete')
    for (const action of ['Edit', 'Details', 'Delete']) {
      await page.keyboard.press('Tab')
      await expect(focused).toHaveAttribute('href', `/Catalog/${action}/${items[0].Id}`)
    }
    await page.keyboard.press('Tab') // the links' cell of the second row
    await page.keyboard.press('Tab')
    await expect(focused).toHaveAttribute('href', `/Catalog/Edit/${items[1].Id}`)

    await page.keyboard.press('Shift+Tab')
    await page.keyboard.press('Shift+Tab')
    await expect(focused).toHaveAttribute('href', `/Catalog/Delete/${items[0].Id}`)

    // One cell and three links per row; the stop after the last row's links is the pager.
    for (let stop = 0; stop < (items.length - 1) * 4; stop++) {
      await page.keyboard.press('Tab')
    }
    await expect(focused).toHaveAttribute('href', `/Catalog/Delete/${items[items.length - 1].Id}`)
    await page.keyboard.press('Tab')
    await expect(focused).toHaveText('Previous')
  })

  test.describe('in a window with room for the grid (P3-4)', () => {
    // About five of the 68px rows fit here (P3-8); in 1920×1080 eleven of the seed's twelve would.
    test.use({ viewport: { width: 1280, height: 720 } })

    test('the page does not scroll; the grid scrolls its rows under a fixed header row', async ({ page, request }) => {
      const all = await getPage(request, 50, 0)
      test.skip(all.Data.length < 8, 'needs more rows than fit into the grid (the seed has twelve items)')
      const rows = page.locator('.ag-row')
      const viewport = page.locator('.esh-grid [role="grid"]') // the element that scrolls
      const scrolls = (element: Element) => element.scrollHeight > element.clientHeight

      await page.goto(app('/?pageSize=50&pageIndex=0'))
      await expect(rows).toHaveCount(all.Data.length)
      await expect(rows.last().locator('.esh-thumbnail')).toHaveJSProperty('complete', true)

      expect(await page.locator('html').evaluate(scrolls)).toBe(false)
      expect(await viewport.evaluate(scrolls)).toBe(true)
      await expect(rows.first()).toBeInViewport()
      await expect(rows.last()).not.toBeInViewport()

      await viewport.evaluate((element) => element.scrollTo(0, element.scrollHeight))

      await expect(rows.last()).toBeInViewport()
      await expect(rows.first()).not.toBeInViewport()
      await expect(page.getByRole('columnheader', { name: 'Name', exact: true })).toBeInViewport({ ratio: 1 })
      await expect(page.getByRole('link', { name: 'Create New' })).toBeInViewport({ ratio: 1 })
      await expect(page.locator('span.esh-pager-item')).toBeInViewport({ ratio: 1 })
      expect(await page.locator('html').evaluate((html) => html.scrollTop)).toBe(0)
    })

    test('the other pages keep their layout', async ({ page, request }) => {
      const item = (await getPage(request)).Data[0]

      await page.goto(app('/'))
      await expect(page.locator('body')).toHaveClass(/esh-fill/)
      await page.locator('.ag-row').first().getByRole('link', { name: 'Details' }).click()

      await expect(page.locator('dl.dl-horizontal dd').first()).toHaveText(item.Name)
      await expect(page.locator('body')).not.toHaveClass(/esh-fill/)
    })
  })

  test('in a low window the grid keeps a minimum height and the page scrolls (P3-4)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 480 })

    await page.goto(app('/'))
    await expect(page.locator('.ag-row').first()).toBeVisible()

    expect(await page.locator('.esh-grid').evaluate((grid) => grid.getBoundingClientRect().height)).toBe(300)
    expect(await page.locator('html').evaluate((html) => html.scrollHeight > html.clientHeight)).toBe(true)
  })

  test('pages forward and back with Next and Previous', async ({ page, request }) => {
    const pageSize = 5
    const first = await getPage(request, pageSize, 0)
    const second = await getPage(request, pageSize, 1)
    test.skip(first.TotalPages < 3, 'needs at least three pages of five (the seed has twelve items)')
    const previous = page.locator('a.esh-pager-item', { hasText: 'Previous' })
    const next = page.locator('a.esh-pager-item', { hasText: 'Next' })

    await page.goto(app(`/?pageSize=${pageSize}&pageIndex=0`))
    await expect(page.locator('.ag-row')).toHaveCount(pageSize)
    await expect(previous).toHaveClass(hidden)
    await expect(previous).toHaveCSS('opacity', '0') // how site.css hides it; it keeps its place
    await expect(next).not.toHaveClass(hidden)

    await next.click()
    await expect(page).toHaveURL(/\?pageSize=5&pageIndex=1$/)
    await expect(page.locator('span.esh-pager-item')).toHaveText(
      `Showing 5 of ${first.TotalItems} products - Page 2 - ${first.TotalPages}`,
    )
    await expect(page.locator('.ag-row').first().locator('[role="gridcell"]').nth(1)).toHaveText(second.Data[0].Name)
    await expect(previous).not.toHaveClass(hidden)

    await previous.click()
    await expect(page).toHaveURL(/pageIndex=0$/)
    await expect(page.locator('.ag-row').first().locator('[role="gridcell"]').nth(1)).toHaveText(first.Data[0].Name)

    await page.goBack()
    await expect(page.locator('span.esh-pager-item')).toContainText('Page 2')
  })

  test('hides Next on the last page', async ({ page, request }) => {
    const pageSize = 5
    const first = await getPage(request, pageSize, 0)
    const lastIndex = first.TotalPages - 1
    const last = await getPage(request, pageSize, lastIndex)

    await page.goto(app(`/?pageSize=${pageSize}&pageIndex=${lastIndex}`))

    await expect(page.locator('.ag-row')).toHaveCount(last.Data.length)
    // The number of rows, not the page size (P3-9).
    await expect(page.locator('span.esh-pager-item')).toHaveText(
      `Showing ${last.Data.length} of ${first.TotalItems} products - Page ${lastIndex + 1} - ${first.TotalPages}`,
    )
    await expect(page.locator('a.esh-pager-item', { hasText: 'Next' })).toHaveClass(hidden)
    await expect(page.locator('a.esh-pager-item', { hasText: 'Previous' })).not.toHaveClass(hidden)
  })

  test('shows the last page for a page index beyond it and corrects the URL (P3-9)', async ({ page, request }) => {
    const pageSize = 5
    const first = await getPage(request, pageSize, 0)
    const lastIndex = first.TotalPages - 1
    const last = await getPage(request, pageSize, lastIndex)

    await page.goto(app('/'))
    await expect(page.locator('.ag-row').first()).toBeVisible()
    await page.goto(app(`/?pageSize=${pageSize}&pageIndex=${first.TotalPages + 2}`))

    await expect(page).toHaveURL(new RegExp(`\\?pageSize=${pageSize}&pageIndex=${lastIndex}$`))
    await expect(page.locator('.ag-row')).toHaveCount(last.Data.length)
    await expect(page.locator('.ag-row').first().locator('[role="gridcell"]').nth(1)).toHaveText(last.Data[0].Name)
    await expect(page.locator('span.esh-pager-item')).toHaveText(
      `Showing ${last.Data.length} of ${first.TotalItems} products - Page ${lastIndex + 1} - ${first.TotalPages}`,
    )
    await expect(page.locator('a.esh-pager-item', { hasText: 'Next' })).toHaveClass(hidden)

    // The URL was replaced: Back leaves the list's last page for the page before, not the empty one.
    await page.goBack()
    await expect(page).toHaveURL(appUrl())
  })

  test('says so for invalid paging values (P2-14)', async ({ page }) => {
    await page.goto(app('/?pageSize=0'))

    await expect(page.getByRole('alert')).toHaveText('Invalid paging values.')
    await expect(page.locator('[role="grid"]')).toHaveCount(0)
  })
})
