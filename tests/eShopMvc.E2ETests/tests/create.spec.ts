import { app, appUrl, field, findByName } from '../support/app.ts'
import { expect, test } from '../support/fixtures.ts'

test.describe('Create page', () => {
  test('starts like the Razor form', async ({ page }) => {
    await page.goto(app('/'))
    await page.getByRole('link', { name: 'Create New' }).click()

    await expect(page).toHaveURL(appUrl('/Catalog/Create'))
    await expect(page.getByRole('heading', { level: 2 })).toHaveText('Create')
    await expect(page).toHaveTitle('Create - Catalog manager (MVC)')
    await expect(page.locator('form label')).toHaveText([
      'Name', 'Description', 'Brand', 'Type', 'Price', 'Picture name', 'Stock', 'Restock', 'Max stock',
    ])
    await expect(field(page, 'Price')).toHaveValue('0.00')
    await expect(field(page, 'Stock')).toHaveValue('0')
    await expect(field(page, 'Brand').locator('option')).not.toHaveCount(0)
    await expect(page.getByText('Uploading images not allowed for this version.')).toBeVisible()
  })

  test('shows validation messages and creates the item once they are fixed', async ({ page, request, newName }) => {
    await page.goto(app('/Catalog/Create'))
    const create = page.getByRole('button', { name: '[ Create ]' })

    // Checked in the browser, as the Razor form did (P2-16): nothing is sent, the cursor goes to the field.
    let posts = 0
    page.on('request', (sent) => {
      posts += sent.method() === 'POST' ? 1 : 0
    })
    await create.click()
    await expect(page.locator('#Name + span')).toHaveText('The Name field is required.')
    await expect(field(page, 'Name')).toBeFocused()

    // The message goes while the field is corrected.
    await field(page, 'Name').pressSequentially(newName)
    await expect(page.locator('#Name + span')).toHaveText('')

    await field(page, 'Description').fill('Created through the form')
    await field(page, 'Price').fill('1.999')
    await field(page, 'Stock').fill('-1')
    await create.click()
    await expect(page.locator('#Price + span')).toHaveText(
      'The field Price must be a positive number with maximum two decimals.',
    )
    await expect(page.locator('#AvailableStock + span')).toHaveText('The field Stock must be between 0 and 10 million.')
    await expect(field(page, 'Price')).toBeFocused()

    for (const [price, message] of [
      ['abc', 'The field Price must be a positive number with maximum two decimals.'],
      ['12.', 'The field Price must be a number.'],
      ['99999999999', 'The field Price must be between 0 and 1000000.'],
    ]) {
      await field(page, 'Price').fill(price)
      await create.click()
      await expect(page.locator('#Price + span')).toHaveText(message)
    }
    expect(posts).toBe(0)

    // Checked by the server only: a stock that isn't a whole number.
    await field(page, 'Price').fill('12.34')
    await field(page, 'Stock').fill('1.5')
    await create.click()
    await expect(page.locator('#AvailableStock + span')).toHaveText("The value '1.5' is not valid for Stock.")
    await expect(field(page, 'Stock')).toBeFocused()
    await expect(field(page, 'Name')).toHaveValue(newName)
    expect(posts).toBe(1)
    expect(await findByName(request, newName)).toBeNull()

    // Valid: saved, then the list.
    const brand = await field(page, 'Brand').locator('option').nth(1).textContent()
    await field(page, 'Brand').selectOption({ label: brand! })
    await field(page, 'Price').fill('12.34')
    await field(page, 'Stock').fill('7')
    await create.click()
    await expect(page).toHaveURL(appUrl())
    await expect(page.locator('tbody tr').first()).toBeVisible()

    const created = await findByName(request, newName)
    expect(created).toMatchObject({
      Description: 'Created through the form',
      Price: 12.34,
      PictureFileName: 'dummy.png',
      AvailableStock: 7,
      CatalogBrand: { Brand: brand },
    })

    await page.goto(app('/?pageSize=1000'))
    const row = page.locator('tbody tr', { hasText: newName })
    await expect(row.locator('td.esh-table-price')).toHaveText('$12.34')
    await expect(row.locator('td').nth(3)).toHaveText(brand!)
  })

  test('Cancel returns to the list without creating anything', async ({ page, request, newName }) => {
    await page.goto(app('/Catalog/Create'))
    await field(page, 'Name').fill(newName)
    await page.getByRole('link', { name: '[ Cancel ]' }).click()

    await expect(page).toHaveURL(appUrl())
    expect(await findByName(request, newName)).toBeNull()
  })
})
