import { Link } from 'react-router'
import type { CatalogItem } from '../api.ts'
import { formatPrice } from '../format.ts'

// The Razor CatalogTable.cshtml: same columns, labels ([Display] names of CatalogItem) and classes.
export function CatalogTable({ items }: { items: CatalogItem[] }) {
  return (
    <table className="table">
      <thead>
        <tr className="esh-table-header">
          <th></th>
          <th>Name</th>
          <th>Description</th>
          <th>Brand</th>
          <th>Type</th>
          <th>Price</th>
          <th>Picture name</th>
          <th>Stock</th>
          <th>Restock</th>
          <th>Max stock</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        {items.map((item) => (
          <tr key={item.Id}>
            <td>
              <img className="esh-thumbnail" src={item.PictureUri ?? undefined} />
            </td>
            <td>{item.Name}</td>
            <td>{item.Description}</td>
            <td>{item.CatalogBrand?.Brand}</td>
            <td>{item.CatalogType?.Type}</td>
            <td className="esh-table-price">{formatPrice(item.Price)}</td>
            <td>{item.PictureFileName}</td>
            <td>{item.AvailableStock}</td>
            <td>{item.RestockThreshold}</td>
            <td>{item.MaxStockThreshold}</td>
            <td>
              <Link className="esh-table-link" to={`/Catalog/Edit/${item.Id}`}>
                Edit
              </Link>
              {' | '}
              <Link className="esh-table-link" to={`/Catalog/Details/${item.Id}`}>
                Details
              </Link>
              {' | '}
              <Link className="esh-table-link" to={`/Catalog/Delete/${item.Id}`}>
                Delete
              </Link>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
