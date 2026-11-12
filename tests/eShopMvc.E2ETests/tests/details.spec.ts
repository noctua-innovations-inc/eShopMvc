import { app, appUrl, field, getPage } from '../support/app.ts'
import { expect, test } from '../support/fixtures.ts'

test.describe('Details page', () => {
  test('opens from the list and shows the item', async ({ page, request }) => {
    const item = (await getPage(request)).Data[0]

    await page.goto(app('/'))
    await page.locator('.ag-row').first().getByRole('link', { name: 'Details' }).click()

    await expect(page).toHaveURL(appUrl(`/Catalog/Details/${item.Id}`))
    await expect(page.getByRole('heading', { level: 2 })).toHaveText('Details')
    await expect(page).toHaveTitle('Details - Catalog manager (MVC)')
    await expect(page.locator('dl.dl-horizontal dt')).toHaveText([
      'Name', 'Description', 'Brand', 'Type', 'Price', 'Picture name', 'Stock', 'Restock', 'Max stock',
    ])
    await expect(page.locator('dl.dl-horizontal dd')).toHaveText([
      item.Name,
      item.Description ?? '',
      item.CatalogBrand.Brand,
      item.CatalogType.Type,
      `$${item.Price.toFixed(2)}`,
      item.PictureFileName,
      String(item.AvailableStock),
      String(item.RestockThreshold),
      String(item.MaxStockThreshold),
    ])
    const picture = page.locator('img.esh-picture')
    await expect(picture).toHaveJSProperty('complete', true)
    expect(await picture.evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(0)
  })

  test('goes back to the list and on to Edit', async ({ page, request }) => {
    const item = (await getPage(request)).Data[0]

    await page.goto(app(`/Catalog/Details/${item.Id}`))
    await page.getByRole('link', { name: 'Back to List' }).click()
    await expect(page).toHaveURL(appUrl())
    await expect(page.locator('.ag-row').first()).toBeVisible()

    await page.goBack()
    await page.getByRole('link', { name: 'Edit' }).click()
    await expect(page).toHaveURL(appUrl(`/Catalog/Edit/${item.Id}`))
    await expect(field(page, 'Name')).toHaveValue(item.Name)
  })

  for (const id of ['2147483647', 'abc']) {
    test(`says so for the unknown id ${id} (P2-15)`, async ({ page }) => {
      await page.goto(app(`/Catalog/Details/${id}`))

      await expect(page.getByRole('alert')).toHaveText('The catalog item was not found.')
      await expect(page.getByRole('link', { name: 'Back to List' })).toBeVisible()
    })
  }
})
