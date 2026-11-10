import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { ApiError, deleteCatalogItem } from '../api.ts'
import { CatalogItemMessage, CatalogItemView } from '../components/CatalogItemView.tsx'
import { itemNotFound, useCatalogItem, useDocumentTitle } from '../hooks.ts'

// The Razor Catalog/Delete.cshtml. Its form post is a DELETE on the API here; afterwards the list,
// as the Razor action redirected to Index.
export function CatalogDelete() {
  const state = useCatalogItem(useParams().id)
  const navigate = useNavigate()
  const [deleting, setDeleting] = useState(false)
  const [deleteError, setDeleteError] = useState<string | null>(null)
  useDocumentTitle('Delete')

  async function handleSubmit(event: FormEvent, id: number) {
    event.preventDefault()
    setDeleting(true)
    setDeleteError(null)
    try {
      await deleteCatalogItem(id)
    } catch (error) {
      // 404: deleted in the meantime (R-15). Anything else includes a rejected antiforgery token (400).
      const notFound = error instanceof ApiError && error.status === 404
      setDeleteError(notFound ? itemNotFound : 'The catalog item could not be deleted.')
      setDeleting(false)
      return
    }
    await navigate('/')
  }

  return (
    <>
      <h2 className="esh-body-title">Delete</h2>

      <div className="container">
        {state.status === 'failed' && <CatalogItemMessage message={state.message} />}
        {state.status === 'loaded' && (
          <>
            <h3>Are you sure you want to delete this?</h3>
            <CatalogItemView item={state.item}>
              <dt></dt>
              <dd className="text-right esh-button-actions">
                {deleteError && <p role="alert">{deleteError}</p>}
                <form method="post" onSubmit={(event) => void handleSubmit(event, state.item.Id)}>
                  <Link className="btn esh-button esh-button-secondary" to="/">
                    [ Cancel ]
                  </Link>{' '}
                  <button type="submit" className="btn esh-button esh-button-primary" disabled={deleting}>
                    [ Delete ]
                  </button>
                </form>
              </dd>
            </CatalogItemView>
          </>
        )}
      </div>
    </>
  )
}
