import { app, appUrl, getItem } from '../support/app.ts'
import { expect, test } from '../support/fixtures.ts'

test.describe('Delete page', () => {
  test('Cancel keeps the item', async ({ page, request, item }) => {
    await page.goto(app('/?pageSize=1000'))
    await page.locator('tbody tr', { hasText: item.Name }).getByRole('link', { name: 'Delete' }).click()

    await expect(page).toHaveURL(appUrl(`/Catalog/Delete/${item.Id}`))
    await expect(page.getByRole('heading', { level: 2 })).toHaveText('Delete')
    await expect(page).toHaveTitle('Delete - Catalog manager (MVC)')
    await expect(page.getByRole('heading', { level: 3 })).toHaveText('Are you sure you want to delete this?')
    await expect(page.locator('dl.dl-horizontal dd').first()).toHaveText(item.Name)

    await page.getByRole('link', { name: '[ Cancel ]' }).click()
    await expect(page).toHaveURL(appUrl())
    expect(await getItem(request, item.Id)).not.toBeNull()
  })

  test('deletes the item and returns to the list', async ({ page, request, item }) => {
    await page.goto(app(`/Catalog/Delete/${item.Id}`))
    await page.getByRole('button', { name: '[ Delete ]' }).click()

    await expect(page).toHaveURL(appUrl())
    await expect(page.locator('tbody tr').first()).toBeVisible()
    expect(await getItem(request, item.Id)).toBeNull()

    await page.goto(app('/?pageSize=1000'))
    await expect(page.locator('tbody tr').first()).toBeVisible()
    await expect(page.locator('tbody tr', { hasText: item.Name })).toHaveCount(0)

    // The page of an item that is gone (P2-15).
    await page.goto(app(`/Catalog/Delete/${item.Id}`))
    await expect(page.getByRole('alert')).toHaveText('The catalog item was not found.')
    await expect(page.getByRole('button')).toHaveCount(0)
  })
})
