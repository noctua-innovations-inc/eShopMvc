import type { ReactNode } from 'react'
import { Link } from 'react-router'
import type { CatalogItem } from '../api.ts'
import { formatPrice } from '../format.ts'

// The picture and field list shared by the Razor Details.cshtml and Delete.cshtml. `children` are
// further entries of the list (Delete puts its buttons there).
export function CatalogItemView({ item, children }: { item: CatalogItem; children?: ReactNode }) {
  return (
    <div className="row">
      <img className="col-md-6 esh-picture" src={item.PictureUri ?? undefined} />
      <dl className="col-md-6 dl-horizontal">
        <dt>Name</dt>
        <dd>{item.Name}</dd>
        <dt>Description</dt>
        <dd>{item.Description}</dd>
        <dt>Brand</dt>
        <dd>{item.CatalogBrand?.Brand}</dd>
        <dt>Type</dt>
        <dd>{item.CatalogType?.Type}</dd>
        <dt>Price</dt>
        <dd>{formatPrice(item.Price)}</dd>
        <dt>Picture name</dt>
        <dd>{item.PictureFileName}</dd>
        <dt>Stock</dt>
        <dd>{item.AvailableStock}</dd>
        <dt>Restock</dt>
        <dd>{item.RestockThreshold}</dd>
        <dt>Max stock</dt>
        <dd>{item.MaxStockThreshold}</dd>
        {children}
      </dl>
    </div>
  )
}

// What the Details and Delete pages show instead of an item that is missing or failed to load.
export function CatalogItemMessage({ message }: { message: string }) {
  return (
    <>
      <p role="alert">{message}</p>
      <p className="esh-link-list esh-link-item--margin">
        <Link className="esh-link-item" to="/">
          Back to List
        </Link>
      </p>
    </>
  )
}
