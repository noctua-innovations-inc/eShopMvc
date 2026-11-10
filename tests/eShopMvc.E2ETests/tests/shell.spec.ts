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

  test('says so for an unknown route (P2-18)', async ({ page }) => {
    await page.goto(app('/Nope'))

    await expect(page.getByRole('alert')).toHaveText('Page not found.')
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Catalog manager (MVC)')
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

  test('shows the same values as the Razor footer of the same session', async ({ page }) => {
    test.skip(app('') === '', 'after the cutover there is no Razor page to compare with')

    await page.goto(app('/'))
    const footer = page.locator('footer small')
    await expect(footer).toHaveText(footerText)
    const react = await footer.textContent()

    await page.goto('/Catalog/Details/1')
    await expect(footer).toHaveText(react!)
  })
})
