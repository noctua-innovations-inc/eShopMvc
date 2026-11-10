import { Link, useParams } from 'react-router'
import { CatalogItemMessage, CatalogItemView } from '../components/CatalogItemView.tsx'
import { useCatalogItem, useDocumentTitle } from '../hooks.ts'

// The Razor Catalog/Details.cshtml.
export function CatalogDetails() {
  const state = useCatalogItem(useParams().id)
  useDocumentTitle('Details')

  return (
    <>
      <h2 className="esh-body-title">Details</h2>

      <div className="container">
        {state.status === 'failed' && <CatalogItemMessage message={state.message} />}
        {state.status === 'loaded' && (
          <>
            <CatalogItemView item={state.item} />
            <p className="esh-link-list esh-link-item--margin">
              <Link className="esh-link-item" to={`/Catalog/Edit/${state.item.Id}`}>
                Edit
              </Link>
              {' | '}
              <Link className="esh-link-item" to="/">
                Back to List
              </Link>
            </p>
          </>
        )}
      </div>
    </>
  )
}
