import { CatalogItemForm } from '../components/CatalogItemForm.tsx'
import { CatalogItemMessage } from '../components/CatalogItemView.tsx'
import { useDocumentTitle, useFormLists } from '../hooks.ts'

// The Razor Catalog/Create.cshtml.
export function CatalogCreate() {
  const lists = useFormLists()
  useDocumentTitle('Create')

  return (
    <>
      <h2 className="esh-body-title">Create</h2>

      {lists.status === 'failed' && (
        <div className="container">
          <CatalogItemMessage message="The catalog could not be loaded." />
        </div>
      )}
      {lists.status === 'loaded' && <CatalogItemForm brands={lists.brands} types={lists.types} />}
    </>
  )
}
