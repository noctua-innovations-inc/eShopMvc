import { Link, Outlet } from 'react-router'
import { Footer } from './Footer.tsx'

// Decision P2-11: the markup and texts of the Razor _Layout.cshtml (header, banner, footer), with the
// existing CSS linked from index.html.
export function Layout() {
  return (
    <>
      <header className="navbar navbar-light navbar-static-top">
        <div className="esh-header-brand">
          <Link to="/">
            <img src="/images/brand.png" />
          </Link>
        </div>
      </header>
      <section className="esh-app-hero">
        <div className="container esh-header">
          <h1 className="esh-header-title">
            Catalog manager <span>(MVC)</span>
          </h1>
        </div>
      </section>

      <Outlet />

      <Footer />
    </>
  )
}
