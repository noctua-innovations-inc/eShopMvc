import type { CatalogItem, CatalogItemInput, ValidationErrors } from './api.ts'

// The values, checks and messages of the Create and Edit forms (Decisions P2-6 and P2-16).

/** What the form's inputs hold: strings, as typed. */
export interface FormValues {
  Name: string
  Description: string
  CatalogBrandId: string
  CatalogTypeId: string
  Price: string
  PictureFileName: string
  AvailableStock: string
  RestockThreshold: string
  MaxStockThreshold: string
}

export type FieldName = keyof FormValues
export type FieldErrors = Partial<Record<FieldName, string>>

/** The [Display] names of CatalogItem, as the server's messages use them. */
export const labels: Record<FieldName, string> = {
  Name: 'Name',
  Description: 'Description',
  CatalogBrandId: 'Brand',
  CatalogTypeId: 'Type',
  Price: 'Price',
  PictureFileName: 'Picture name',
  AvailableStock: 'Stock',
  RestockThreshold: 'Restock',
  MaxStockThreshold: 'Max stock',
}

const integerFields = ['AvailableStock', 'RestockThreshold', 'MaxStockThreshold'] as const

/** The Razor Create form's start values (`new CatalogItem()`); brand and type: the first option. */
export function newItemValues(): FormValues {
  return {
    Name: '',
    Description: '',
    CatalogBrandId: '',
    CatalogTypeId: '',
    Price: '0.00',
    PictureFileName: 'dummy.png',
    AvailableStock: '0',
    RestockThreshold: '0',
    MaxStockThreshold: '0',
  }
}

/** The Razor Edit form's start values; the price with two decimals, as its text box showed it. */
export function itemValues(item: CatalogItem): FormValues {
  return {
    Name: item.Name ?? '',
    Description: item.Description ?? '',
    CatalogBrandId: String(item.CatalogBrandId),
    CatalogTypeId: String(item.CatalogTypeId),
    Price: item.Price.toFixed(2),
    PictureFileName: item.PictureFileName ?? '',
    AvailableStock: String(item.AvailableStock),
    RestockThreshold: String(item.RestockThreshold),
    MaxStockThreshold: String(item.MaxStockThreshold),
  }
}

/**
 * The checks made before sending (P2-16): only that Name is there and that the number fields hold
 * numbers, which JSON needs. The messages are the Razor UI's. Ranges, decimals and the picture name
 * are the server's business.
 */
export function validate(values: FormValues): FieldErrors {
  const errors: FieldErrors = {}
  if (values.Name.trim() === '') {
    errors.Name = required('Name')
  }

  const price = values.Price.trim()
  if (price === '') {
    errors.Price = required('Price')
  } else if (!/^-?(\d+\.?\d*|\.\d+)$/.test(price)) {
    errors.Price = notANumber('Price')
  }

  for (const field of integerFields) {
    const value = values[field].trim()
    if (value === '') {
      errors[field] = required(field)
    } else if (!/^-?\d+$/.test(value)) {
      errors[field] = notANumber(field)
    }
  }
  return errors
}

/**
 * The JSON body for valid values. An empty description is null, as MVC bound the Razor form's empty
 * text box; `OnReorder` is left out (false), as that form never posted it.
 */
export function toInput(values: FormValues): CatalogItemInput {
  return {
    Name: values.Name,
    Description: values.Description === '' ? null : values.Description,
    Price: Number(values.Price),
    PictureFileName: values.PictureFileName,
    CatalogTypeId: Number(values.CatalogTypeId),
    CatalogBrandId: Number(values.CatalogBrandId),
    AvailableStock: Number(values.AvailableStock),
    RestockThreshold: Number(values.RestockThreshold),
    MaxStockThreshold: Number(values.MaxStockThreshold),
  }
}

/**
 * Sorts a 400's messages into those of the form's fields (the first one each, as the Razor
 * ValidationMessageFor showed) and the rest, for the summary. A value the server could not read at
 * all (key "$.Field", e.g. a number too large for an int) gets MVC's message for that instead of the
 * JSON parser's.
 */
export function sortServerErrors(
  errors: ValidationErrors,
  values: FormValues,
): { fields: FieldErrors; summary: string[] } {
  const fields: FieldErrors = {}
  const summary: string[] = []
  const hasUnreadableField = Object.keys(errors).some((key) => key.startsWith('$.') && key.slice(2) in labels)

  for (const [key, messages] of Object.entries(errors)) {
    const unreadable = key.startsWith('$.')
    const name = unreadable ? key.slice(2) : key
    if (name in labels) {
      const field = name as FieldName
      fields[field] ??= unreadable
        ? `The value '${values[field]}' is not valid for ${labels[field]}.`
        : messages[0]
    } else if (!(key === 'input' && hasUnreadableField)) {
      // "input" only repeats that the body could not be read.
      summary.push(...messages)
    }
  }
  return { fields, summary }
}

function required(field: FieldName): string {
  return `The ${labels[field]} field is required.`
}

function notANumber(field: FieldName): string {
  return `The field ${labels[field]} must be a number.`
}
