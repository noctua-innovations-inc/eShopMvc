import { useParams } from 'react-router'
import { CatalogItemForm } from '../components/CatalogItemForm.tsx'
import { CatalogItemMessage } from '../components/CatalogItemView.tsx'
import { itemNotLoaded, useCatalogItem, useDocumentTitle, useFormLists } from '../hooks.ts'

// The Razor Catalog/Edit.cshtml.
export function CatalogEdit() {
  const state = useCatalogItem(useParams().id)
  const lists = useFormLists()
  useDocumentTitle('Edit')

  // A missing item is reported as such even if the drop-down lists failed too.
  const failure = state.status === 'failed' ? state.message : lists.status === 'failed' ? itemNotLoaded : null

  return (
    <>
      <h2 className="esh-body-title">Edit</h2>

      {failure !== null && (
        <div className="container">
          <CatalogItemMessage message={failure} />
        </div>
      )}
      {failure === null && state.status === 'loaded' && lists.status === 'loaded' && (
        // The key gives another item its own form state.
        <CatalogItemForm key={state.item.Id} item={state.item} brands={lists.brands} types={lists.types} />
      )}
    </>
  )
}
