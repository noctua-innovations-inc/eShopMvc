import { useEffect, useState } from 'react'
import { getSession } from '../api.ts'
import type { SessionInfo } from '../api.ts'

// Decisions R-11 and P2-8: "{MachineName}, {SessionStartTime}" of the visitor's session, from the API
// (the Razor layout read them from the session directly). Empty until loaded or if the call fails.
export function Footer() {
  const [session, setSession] = useState<SessionInfo | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    getSession(controller.signal).then(setSession, () => {})
    return () => controller.abort()
  }, [])

  return (
    <footer className="esh-app-footer">
      <div className="container">
        <article className="row">
          <section className="col-sm-6">
            <img className="esh-app-footer-brand" src="/images/brand_dark.png" />
          </section>
          <section className="col-sm-6">
            <img
              className="esh-app-footer-text hidden-xs"
              src="/images/main_footer_text.png"
              width="335"
              height="26"
              alt="footer text image"
            />
            <br />
            <small>{session && `${session.MachineName}, ${session.SessionStartTime}`}</small>
          </section>
        </article>
      </div>
    </footer>
  )
}
