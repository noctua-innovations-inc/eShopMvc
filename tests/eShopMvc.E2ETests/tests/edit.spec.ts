import { app, appUrl, field, getItem } from '../support/app.ts'
import { expect, test } from '../support/fixtures.ts'

test.describe('Edit page', () => {
  test("starts with the item's values", async ({ page, item }) => {
    await page.goto(app(`/Catalog/Edit/${item.Id}`))

    await expect(page.getByRole('heading', { level: 2 })).toHaveText('Edit')
    await expect(page).toHaveTitle('Edit - Catalog manager (MVC)')
    // The drop-downs' labels are the Razor Edit view's (R-24).
    await expect(page.locator('form label')).toHaveText([
      'Name', 'Description', 'CatalogBrandId', 'CatalogTypeId', 'Price', 'Picture name', 'Stock', 'Restock', 'Max stock',
    ])
    await expect(field(page, 'Name')).toHaveValue(item.Name)
    await expect(field(page, 'Description')).toHaveValue(item.Description!)
    await expect(field(page, 'CatalogBrandId')).toHaveValue(String(item.CatalogBrandId))
    await expect(field(page, 'CatalogTypeId')).toHaveValue(String(item.CatalogTypeId))
    await expect(field(page, 'Price')).toHaveValue('12.34')
    await expect(field(page, 'Stock')).toHaveValue('5')
    await expect(field(page, 'Restock')).toHaveValue('1')
    await expect(field(page, 'Max stock')).toHaveValue('10')
    await expect(field(page, 'Picture name')).toHaveValue('dummy.png')
    await expect(field(page, 'Picture name')).not.toBeEditable()
    await expect(page.locator('img.esh-picture')).toHaveJSProperty('complete', true)
  })

  test('saves the changes and shows them in Details', async ({ page, request, item }) => {
    const renamed = `${item.Name}-edited`

    await page.goto(app(`/Catalog/Edit/${item.Id}`))
    await field(page, 'Name').fill(renamed)
    await field(page, 'Description').fill('')
    await field(page, 'Price').fill('1234.5')
    await field(page, 'Max stock').fill('20')
    await field(page, 'CatalogTypeId').selectOption({ index: 1 })
    const type = await field(page, 'CatalogTypeId').locator('option:checked').textContent()
    await page.getByRole('button', { name: '[ Save ]' }).click()

    await expect(page).toHaveURL(appUrl())
    expect(await getItem(request, item.Id)).toMatchObject({
      Name: renamed,
      Description: null,
      Price: 1234.5,
      MaxStockThreshold: 20,
      AvailableStock: 5,
      PictureFileName: 'dummy.png',
      CatalogType: { Type: type },
    })

    await page.goto(app(`/Catalog/Details/${item.Id}`))
    await expect(page.locator('dl.dl-horizontal dd')).toHaveText([
      renamed, '', item.CatalogBrand.Brand, type!, '$1,234.50', 'dummy.png', '5', '1', '20',
    ])
  })

  test('shows the message for a price out of range and keeps the item unchanged', async ({ page, request, item }) => {
    await page.goto(app(`/Catalog/Edit/${item.Id}`))
    await field(page, 'Price').fill('2000000')
    await page.getByRole('button', { name: '[ Save ]' }).click()

    await expect(page.locator('#Price + span')).toHaveText('The field Price must be between 0 and 1000000.')
    await expect(page).toHaveURL(appUrl(`/Catalog/Edit/${item.Id}`))
    expect(await getItem(request, item.Id)).toMatchObject({ Price: 12.34 })
  })

  test('says so for an unknown id (P2-15)', async ({ page }) => {
    await page.goto(app('/Catalog/Edit/2147483647'))

    await expect(page.getByRole('alert')).toHaveText('The catalog item was not found.')
    await expect(page.locator('form')).toHaveCount(0)
  })
})
