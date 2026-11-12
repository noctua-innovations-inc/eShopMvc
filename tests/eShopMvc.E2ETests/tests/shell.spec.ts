import { app, appUrl, getPage } from '../support/app.ts'
import { expect, test } from '../support/fixtures.ts'

test.describe('Shell', () => {
  test('loads the Razor style sheets and images without errors (P2-11)', async ({ page }) => {
    const failed: string[] = []
    const errors: string[] = []
    page.on('response', (response) => {
      if (response.status() >= 400) {
        failed.push(`${response.status()} ${response.url()}`)
      }
    })
    page.on('pageerror', (error) => errors.push(error.message))
    page.on('console', (message) => {
      if (message.type() === 'error') {
        errors.push(message.text())
      }
    })

    await page.goto(app('/'))
    await expect(page.locator('.ag-row').first()).toBeVisible()
    await expect(page.locator('footer')).toHaveCount(0) // P3-7

    expect(failed).toEqual([])
    expect(errors).toEqual([])
    // The linked style sheets; AG Grid adds its theme as <style> elements, which have no href (P3-5).
    const styleSheets = await page.evaluate(() =>
      [...document.styleSheets]
        .filter((sheet) => sheet.href !== null)
        .map((sheet) => ({ href: new URL(sheet.href!).pathname, rules: sheet.cssRules.length })),
    )
    expect(styleSheets.map((sheet) => sheet.href)).toEqual([
      '/lib/bootstrap/dist/css/bootstrap.min.css', '/css/custom.css', '/css/base.css', '/css/site.css',
    ])
    for (const sheet of styleSheets) {
      expect(sheet.rules, sheet.href).toBeGreaterThan(0)
    }
    for (const image of await page.locator('header img').all()) {
      expect(await image.evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(0)
    }
  })

  test('opens a deep link directly and after a reload (P2-4)', async ({ page, request }) => {
    const item = (await getPage(request)).Data[0]

    await page.goto(app(`/Catalog/Details/${item.Id}`))
    await expect(page.locator('dl.dl-horizontal dd').first()).toHaveText(item.Name)

    await page.reload()
    await expect(page.locator('dl.dl-horizontal dd').first()).toHaveText(item.Name)
  })

  test('the brand image leads to the list', async ({ page, request }) => {
    const item = (await getPage(request)).Data[0]

    await page.goto(app(`/Catalog/Details/${item.Id}`))
    await page.locator('header .esh-header-brand a').click()

    await expect(page).toHaveURL(appUrl())
    await expect(page.locator('.ag-row').first()).toBeVisible()
  })

  for (const path of ['/Nope', '/Catalog/Nope', '/app/']) {
    test(`answers the unknown URL ${path} with a 404, not the shell (P2-9, P2-18)`, async ({ page }) => {
      const response = await page.goto(path)

      expect(response!.status()).toBe(404)
      await expect(page.locator('#root')).toHaveCount(0)
    })
  }

  test('tells the server which page each API call is for (P2-19)', async ({ page, request }) => {
    const item = (await getPage(request)).Data[0]
    const pages: string[] = []
    page.on('request', (sent) => {
      const url = new URL(sent.url())
      if (url.pathname.startsWith('/api/')) {
        pages.push(`${url.pathname} for ${sent.headers()['x-client-page']}`)
      }
    })

    await page.goto(app('/?pageSize=5&pageIndex=0'))
    await page.locator('.ag-row').first().getByRole('link', { name: 'Edit' }).click()
    await expect(page.getByRole('heading', { level: 2 })).toHaveText('Edit')
    await expect(page.locator('select').first()).not.toBeEmpty()

    expect(pages.sort()).toEqual(
      [
        '/api/v1/catalog/items for /',
        `/api/v1/catalog/items/${item.Id} for /Catalog/Edit/${item.Id}`,
        `/api/v1/catalog/brands for /Catalog/Edit/${item.Id}`,
        `/api/v1/catalog/types for /Catalog/Edit/${item.Id}`,
      ].sort(),
    )
  })
})

test.describe('Font sizes (P3-8)', () => {
  test('are one scale on the item pages: 28px banner, 21px page title, 14px text and links', async ({ page, request }) => {
    const item = (await getPage(request)).Data[0]

    await page.goto(app(`/Catalog/Details/${item.Id}`))
    await expect(page.locator('dl.dl-horizontal dd').first()).toHaveText(item.Name)
    await expect(page.getByRole('heading', { level: 1 })).toHaveCSS('font-size', '28px')
    await expect(page.getByRole('heading', { level: 2 })).toHaveCSS('font-size', '21px')
    await expect(page.locator('dl.dl-horizontal dd').first()).toHaveCSS('font-size', '14px')
    for (const name of ['Edit', 'Back to List']) {
      await expect(page.getByRole('link', { name })).toHaveCSS('font-size', '14px')
    }

    await page.goto(app(`/Catalog/Delete/${item.Id}`))
    await expect(page.getByRole('heading', { level: 3 })).toHaveCSS('font-size', '17.5px')
    await expect(page.getByRole('button', { name: '[ Delete ]' })).toHaveCSS('font-size', '14px')

    await page.goto(app(`/Catalog/Edit/${item.Id}`))
    await expect(page.getByRole('heading', { level: 2 })).toHaveCSS('font-size', '21px')
    await expect(page.locator('label').first()).toHaveCSS('font-size', '14px')
    await expect(page.locator('input').first()).toHaveCSS('font-size', '14px')
  })
})

test.describe('Footer', () => {
  test('is gone from every page, and with it the session cookie (P3-7)', async ({ page, context, request }) => {
    const item = (await getPage(request)).Data[0]

    for (const path of ['/', '/Catalog/Create', `/Catalog/Details/${item.Id}`, `/Catalog/Edit/${item.Id}`, `/Catalog/Delete/${item.Id}`]) {
      await page.goto(app(path))
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
      await expect(page.locator('footer')).toHaveCount(0)
    }
    expect((await context.cookies()).map((cookie) => cookie.name)).not.toContain('.AspNetCore.Session')
  })
})
