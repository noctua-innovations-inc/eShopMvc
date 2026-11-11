import { Route, Routes } from 'react-router'
import { Layout } from './components/Layout.tsx'
import { CatalogCreate } from './pages/CatalogCreate.tsx'
import { CatalogDelete } from './pages/CatalogDelete.tsx'
import { CatalogDetails } from './pages/CatalogDetails.tsx'
import { CatalogEdit } from './pages/CatalogEdit.tsx'
import { CatalogIndex } from './pages/CatalogIndex.tsx'

// The routes of the Razor UI (default route {controller=Catalog}/{action=Index}/{id?}). The server
// answers exactly these with the shell (Decision P2-9, Program.cs): keep the two lists the same.
function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<CatalogIndex />} />
        <Route path="Catalog" element={<CatalogIndex />} />
        <Route path="Catalog/Index" element={<CatalogIndex />} />
        <Route path="Catalog/Create" element={<CatalogCreate />} />
        <Route path="Catalog/Edit/:id" element={<CatalogEdit />} />
        <Route path="Catalog/Details/:id" element={<CatalogDetails />} />
        <Route path="Catalog/Delete/:id" element={<CatalogDelete />} />
        <Route path="*" element={<p role="alert">Page not found.</p>} />
      </Route>
    </Routes>
  )
}

export default App
