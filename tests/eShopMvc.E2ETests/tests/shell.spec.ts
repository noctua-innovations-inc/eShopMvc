import { app, appUrl, getPage } from '../support/app.ts'
import { expect, test } from '../support/fixtures.ts'

const footerText = /^.+, \d{1,2}\/\d{1,2}\/\d{4} \d{1,2}:\d{2}:\d{2} (AM|PM)$/

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
    await expect(page.locator('tbody tr').first()).toBeVisible()
    await expect(page.locator('footer small')).toHaveText(footerText)

    expect(failed).toEqual([])
    expect(errors).toEqual([])
    const styleSheets = await page.evaluate(() =>
      [...document.styleSheets].map((sheet) => ({ href: new URL(sheet.href!).pathname, rules: sheet.cssRules.length })),
    )
    expect(styleSheets.map((sheet) => sheet.href)).toEqual([
      '/lib/bootstrap/dist/css/bootstrap.min.css', '/css/custom.css', '/css/base.css', '/css/site.css',
    ])
    for (const sheet of styleSheets) {
      expect(sheet.rules, sheet.href).toBeGreaterThan(0)
    }
    for (const image of await page.locator('header img, footer img').all()) {
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
    await expect(page.locator('tbody tr').first()).toBeVisible()
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
    await page.locator('tbody tr').first().getByRole('link', { name: 'Edit' }).click()
    await expect(page.getByRole('heading', { level: 2 })).toHaveText('Edit')
    await expect(page.locator('select').first()).not.toBeEmpty()

    expect(pages.sort()).toEqual(
      [
        '/api/v1/catalog/items for /',
        '/api/v1/session for /',
        `/api/v1/catalog/items/${item.Id} for /Catalog/Edit/${item.Id}`,
        `/api/v1/catalog/brands for /Catalog/Edit/${item.Id}`,
        `/api/v1/catalog/types for /Catalog/Edit/${item.Id}`,
      ].sort(),
    )
  })
})

test.describe('Footer', () => {
  test("shows the session's machine name and start time and keeps them (R-11, P2-8)", async ({ page }) => {
    await page.goto(app('/'))
    const footer = page.locator('footer small')
    await expect(footer).toHaveText(footerText)
    const first = await footer.textContent()

    // The start time has one-second resolution: a new session would show a later one.
    await page.waitForTimeout(1100)
    await page.locator('tbody tr').first().getByRole('link', { name: 'Details' }).click()
    await expect(page.getByRole('heading', { level: 2 })).toHaveText('Details')
    await expect(footer).toHaveText(first!)

    await page.reload()
    await expect(footer).toHaveText(first!)
  })
})
