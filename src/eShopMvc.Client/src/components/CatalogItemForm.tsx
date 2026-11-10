import { useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import { Link, useNavigate } from 'react-router'
import { ApiError, createCatalogItem, updateCatalogItem } from '../api.ts'
import type { CatalogBrand, CatalogItem, CatalogType } from '../api.ts'
import { itemValues, labels, newItemValues, sortServerErrors, toInput, validate } from '../catalogItemForm.ts'
import type { FieldErrors, FieldName, FormValues } from '../catalogItemForm.ts'
import { itemNotFound } from '../hooks.ts'

interface Props {
  /** The item to edit (the Razor Edit.cshtml); none: a new one (Create.cshtml). */
  item?: CatalogItem
  brands: CatalogBrand[]
  types: CatalogType[]
}

// The form of the Razor Create.cshtml and Edit.cshtml: same fields, classes and ids; the two differ in
// their column widths, the picture and its name box (Edit only) and the Edit labels of the drop-downs
// (R-24). Saving goes to the API (P2-6); afterwards the list, as the Razor actions redirected to Index.
export function CatalogItemForm({ item, brands, types }: Props) {
  const editing = item !== undefined
  const navigate = useNavigate()
  const [values, setValues] = useState<FormValues>(() => (item ? itemValues(item) : newItemValues()))
  const [errors, setErrors] = useState<FieldErrors>({})
  const [summary, setSummary] = useState<string[]>([])
  const [saving, setSaving] = useState(false)

  function set(field: FieldName, value: string) {
    setValues((current) => ({ ...current, [field]: value }))
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    // A new item's drop-downs show their first option until another one is chosen.
    const current: FormValues = {
      ...values,
      CatalogBrandId: values.CatalogBrandId || String(brands[0]?.Id ?? ''),
      CatalogTypeId: values.CatalogTypeId || String(types[0]?.Id ?? ''),
    }
    const clientErrors = validate(current)
    setErrors(clientErrors)
    setSummary([])
    if (Object.keys(clientErrors).length > 0) {
      return
    }

    setSaving(true)
    try {
      if (item) {
        await updateCatalogItem(item.Id, toInput(current))
      } else {
        await createCatalogItem(toInput(current))
      }
    } catch (error) {
      setSaving(false)
      const sorted = error instanceof ApiError && error.errors ? sortServerErrors(error.errors, current) : null
      if (sorted && (Object.keys(sorted.fields).length > 0 || sorted.summary.length > 0)) {
        setErrors(sorted.fields)
        setSummary(sorted.summary)
      } else if (error instanceof ApiError && error.status === 404 && editing) {
        setSummary([itemNotFound]) // deleted in the meantime
      } else {
        setSummary(['The catalog item could not be saved.'])
      }
      return
    }
    await navigate('/')
  }

  function group(field: FieldName, control: ReactNode, label = labels[field], controlClass = fieldClass) {
    return (
      <div className="form-group">
        <label className={labelClass} htmlFor={field}>
          {label}
        </label>
        <div className={controlClass}>
          {control}
          <span className={`text-danger ${errors[field] ? 'field-validation-error' : 'field-validation-valid'}`}>
            {errors[field]}
          </span>
        </div>
      </div>
    )
  }

  function textBox(field: FieldName, type: 'text' | 'number' = 'text') {
    return (
      <input
        className={`form-control text-box single-line${errors[field] ? ' input-validation-error' : ''}`}
        id={field}
        name={field}
        type={type}
        value={values[field]}
        onChange={(event) => set(field, event.target.value)}
      />
    )
  }

  const labelClass = editing ? 'control-label col-md-4' : 'control-label col-md-2'
  const fieldClass = editing ? 'col-md-8' : 'col-md-3'

  const fields = (
    <>
      {summary.length > 0 && (
        <div className="text-danger validation-summary-errors" role="alert">
          <ul>
            {summary.map((message) => (
              <li key={message}>{message}</li>
            ))}
          </ul>
        </div>
      )}

      {group('Name', textBox('Name'))}
      {group('Description', textBox('Description'))}
      {group(
        'CatalogBrandId',
        <select
          className="form-control"
          id="CatalogBrandId"
          name="CatalogBrandId"
          value={values.CatalogBrandId}
          onChange={(event) => set('CatalogBrandId', event.target.value)}
        >
          {brands.map((brand) => (
            <option key={brand.Id} value={brand.Id}>
              {brand.Brand}
            </option>
          ))}
        </select>,
        editing ? 'CatalogBrandId' : undefined,
      )}
      {group(
        'CatalogTypeId',
        <select
          className="form-control"
          id="CatalogTypeId"
          name="CatalogTypeId"
          value={values.CatalogTypeId}
          onChange={(event) => set('CatalogTypeId', event.target.value)}
        >
          {types.map((type) => (
            <option key={type.Id} value={type.Id}>
              {type.Type}
            </option>
          ))}
        </select>,
        editing ? 'CatalogTypeId' : undefined,
      )}
      {group('Price', textBox('Price'))}
      {editing
        ? group(
            'PictureFileName',
            <input
              className="form-control text-box single-line"
              id="PictureFileName"
              name="PictureFileName"
              readOnly
              title="Not allowed for edition"
              type="text"
              value={values.PictureFileName}
            />,
          )
        : group(
            'PictureFileName',
            'Uploading images not allowed for this version.',
            undefined,
            'col-md-4 esh-form-information',
          )}
      {group('AvailableStock', textBox('AvailableStock', 'number'))}
      {group('RestockThreshold', textBox('RestockThreshold', 'number'))}
      {group('MaxStockThreshold', textBox('MaxStockThreshold', 'number'))}

      <div className="form-group">
        <div className={`${editing ? 'col-md-12' : 'col-md-offset-2 col-md-3'} text-right esh-button-actions`}>
          <Link className="btn esh-button esh-button-secondary" to="/">
            [ Cancel ]
          </Link>{' '}
          {editing ? (
            <button type="submit" className="btn esh-button esh-button-primary" disabled={saving}>
              [ Save ]
            </button>
          ) : (
            <input type="submit" value="[ Create ]" className="btn esh-button esh-button-primary" disabled={saving} />
          )}
        </div>
      </div>
    </>
  )

  return (
    // noValidate: the messages are the form's own, as with the Razor UI's jQuery validation.
    <form method="post" noValidate onSubmit={(event) => void handleSubmit(event)}>
      {item ? (
        <div className="container">
          <div className="row">
            <img className="col-md-6 esh-picture" src={item.PictureUri ?? undefined} />
            <div className="col-md-6 form-horizontal">{fields}</div>
          </div>
        </div>
      ) : (
        <div className="form-horizontal">{fields}</div>
      )}
    </form>
  )
}
