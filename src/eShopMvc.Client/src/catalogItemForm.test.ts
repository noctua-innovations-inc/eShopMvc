import { describe, expect, it } from 'vitest'
import { itemValues, newItemValues, sortServerErrors, toInput, validate } from './catalogItemForm.ts'
import type { FormValues } from './catalogItemForm.ts'
import { catalogItem } from './test/fakeApi.ts'

function values(overrides: Partial<FormValues> = {}): FormValues {
  return { ...newItemValues(), Name: 'Mug', CatalogBrandId: '1', CatalogTypeId: '2', ...overrides }
}

describe('validate', () => {
  it('accepts the start values once there is a name', () => {
    expect(validate(values())).toEqual({})
  })

  it.each(['', '   '])('requires a name ("%s")', (name) => {
    expect(validate(values({ Name: name }))).toEqual({ Name: 'The Name field is required.' })
  })

  it.each(['0', '19.5', '19.50', '1.', '.5', ' 7 ', '-1', '1.999', '2000000'])(
    'leaves the price "%s" to the server',
    (price) => {
      expect(validate(values({ Price: price }))).toEqual({})
    },
  )

  it.each(['abc', '1,5', '1e3', '$5', '1.2.3', '-', '.'])('rejects the price "%s" as not a number', (price) => {
    expect(validate(values({ Price: price }))).toEqual({ Price: 'The field Price must be a number.' })
  })

  it('requires a price', () => {
    expect(validate(values({ Price: ' ' }))).toEqual({ Price: 'The Price field is required.' })
  })

  it('requires whole numbers in the stock fields, with their display names', () => {
    expect(validate(values({ AvailableStock: '', RestockThreshold: '1.5', MaxStockThreshold: 'x' }))).toEqual({
      AvailableStock: 'The Stock field is required.',
      RestockThreshold: 'The field Restock must be a number.',
      MaxStockThreshold: 'The field Max stock must be a number.',
    })
  })

  it('leaves stock ranges to the server', () => {
    expect(validate(values({ AvailableStock: '-1', MaxStockThreshold: '99999999999' }))).toEqual({})
  })
})

describe('toInput', () => {
  it('sends numbers as numbers, an empty description as null and no OnReorder of its own', () => {
    const input = toInput(values({ Price: '19.50', AvailableStock: '100', Description: '' }))

    expect(input).toEqual({
      Name: 'Mug',
      Description: null,
      Price: 19.5,
      PictureFileName: 'dummy.png',
      CatalogTypeId: 2,
      CatalogBrandId: 1,
      AvailableStock: 100,
      RestockThreshold: 0,
      MaxStockThreshold: 0,
    })
  })
})

describe('itemValues', () => {
  it('shows the price with two decimals and null texts as empty', () => {
    const form = itemValues(catalogItem({ Price: 19.5, Description: null, CatalogBrandId: 2, AvailableStock: 100 }))

    expect(form).toMatchObject({ Price: '19.50', Description: '', CatalogBrandId: '2', AvailableStock: '100' })
  })
})

describe('sortServerErrors', () => {
  it('takes the first message of each field and puts unknown keys in the summary', () => {
    const sorted = sortServerErrors(
      { Name: ['The Name field is required.', 'second'], PictureFileName: ['bad name'], Other: ['a', 'b'] },
      values(),
    )

    expect(sorted).toEqual({
      fields: { Name: 'The Name field is required.', PictureFileName: 'bad name' },
      summary: ['a', 'b'],
    })
  })

  it("replaces the JSON parser's message for an unreadable value with MVC's", () => {
    const sorted = sortServerErrors(
      {
        input: ['The input field is required.'],
        '$.AvailableStock': ['The JSON value could not be converted to System.Int32. Path: $.AvailableStock …'],
      },
      values({ AvailableStock: '99999999999' }),
    )

    expect(sorted).toEqual({
      fields: { AvailableStock: "The value '99999999999' is not valid for Stock." },
      summary: [],
    })
  })

  it('keeps "input" when no field explains it', () => {
    const sorted = sortServerErrors({ input: ['The input field is required.'], $: ['Bad JSON.'] }, values())

    expect(sorted).toEqual({ fields: {}, summary: ['The input field is required.', 'Bad JSON.'] })
  })
})
