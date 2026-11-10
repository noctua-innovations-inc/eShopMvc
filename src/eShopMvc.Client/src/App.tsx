import { Route, Routes } from 'react-router'
import { Layout } from './components/Layout.tsx'
import { CatalogDelete } from './pages/CatalogDelete.tsx'
import { CatalogDetails } from './pages/CatalogDetails.tsx'
import { CatalogIndex } from './pages/CatalogIndex.tsx'

// The routes of the Razor UI (default route {controller=Catalog}/{action=Index}/{id?}), relative to
// the Router's basename (Decision P2-4: /app until the cutover).
function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<CatalogIndex />} />
        <Route path="Catalog" element={<CatalogIndex />} />
        <Route path="Catalog/Index" element={<CatalogIndex />} />
        <Route path="Catalog/Details/:id" element={<CatalogDetails />} />
        <Route path="Catalog/Delete/:id" element={<CatalogDelete />} />
        <Route path="*" element={<p role="alert">Page not found.</p>} />
      </Route>
    </Routes>
  )
}

export default App
