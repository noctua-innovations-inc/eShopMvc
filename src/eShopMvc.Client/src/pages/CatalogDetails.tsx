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
              {/* Until step 6 this is the Razor page, outside the React app: a plain link. */}
              <a className="esh-link-item" href={`/Catalog/Edit/${state.item.Id}`}>
                Edit
              </a>
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
