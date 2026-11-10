# Phase 2: replace the Razor UI with React

> **Current phase.** Read this file before starting any task. The project-wide rules in `AGENTS.md`
> still apply; this file adds the goal, decisions and steps of phase 2. Phase 1 decisions
> (R-*, D*, L*, D-*) are in `docs/agents/phase-01-port.md` and stay in force unless a P2 decision
> below changes one of them.

## Goal
Replace the server-rendered Razor catalog pages of `src/eShopMvc.Web` with a **React** single-page
app (TypeScript, Vite, React Router), served by the same ASP.NET Core app, backed by a new JSON
catalog API. Keep the same pages, URLs, look and behavior as the Razor UI at the end of phase 1.
Afterwards the Razor catalog views are gone and the app still runs locally and in the Linux container.

**Reference behavior** is the Razor UI as of the end of phase 1 (commit `e93969e`), plus the
deliberate differences in `docs/parity.md`. `legacy/` stays read-only and is no longer the parity
target for the UI; the smoke tests can still run against it.

## Non-goals (ask before doing any of these)
- Visual redesign, Bootstrap 5, a component library, CSS-in-JS (R-17: major upgrades are a separate step).
- Server-side rendering, Next.js or another meta-framework.
- State management or data-fetching libraries (Redux, TanStack Query, …); use React state and `fetch`.
- Authentication, Azure deployment, database or EF Core changes.
- Changes to `/api/brands`, `/api/files`, `/api/v1/files`, `/items/{id}/pic` or the health endpoints.

## Phase 2 rules (in addition to the ground rules in AGENTS.md)
- Steps follow the order below; one step at a time, plan first for multi-file steps.
- After every step, in addition to `dotnet build` and the smoke tests: once `src/eShopMvc.Client`
  exists, `npm ci`, `npm run build` (includes type checking) and `npm test` must pass there; once
  the Playwright project exists, its tests must pass against the running app.
- **npm packages:** only the ones listed under P2-5. Ask before adding any other. Exact versions
  (`save-exact`, no `^`/`~`), `package-lock.json` committed, `npm ci` in scripts and the Dockerfile.
- **NuGet packages:** none are expected for this phase (antiforgery, Problem Details and the SPA
  fallback are built in). Ask as usual if one turns out to be needed.
- **Routes:** only the changes listed in P2-4 and P2-9.
- Every deliberate difference from the Razor UI gets a P2 decision here and a test, as in phase 1.
- Checks by hand that write data (POST/PUT/DELETE with curl or PowerShell) run against a second
  instance in mock-data mode on another port (`dotnet run --project src/eShopMvc.Web --no-build --
  --Catalog:UseMockData=true --urls http://localhost:5071`), never against the development database.
- Every npm package a step installs is named in that step's plan, even if P2-5 already approves it.
- New API logic (mapping, validation, paging, status codes) gets C# unit tests and in-process
  integration tests (`WebApplicationFactory`, D14). React components with real logic (pager,
  form validation, error display) get Vitest tests; pure markup does not need them.
- Commands and layout entries that stay in use after the phase go into `AGENTS.md` (Commands,
  Repository layout) in the step that creates them. Phase-only notes stay here.

## Decisions
- **P2-1 Hosting:** same app, same origin. The ASP.NET Core app serves the built React files from
  `wwwroot` and the API; one container, no CORS, the antiforgery and session cookies keep working.
- **P2-2 Source layout:** React source in `src/eShopMvc.Client/` (sibling of `eShopMvc.Web`, so
  MSBuild and `dotnet watch` never scan `node_modules`). Vite builds into
  `src/eShopMvc.Web/wwwroot/app/` (`base: '/app/'`), which is git-ignored. `dotnet build` does not
  run npm; the Dockerfile does (P2-10).
- **P2-3 Development:** Vite dev server (http://localhost:5173) with a proxy to the ASP.NET Core app
  (http://localhost:5070) for `/api`, `/items`, `/health`, `/css`, `/lib`, `/images`, `/fonts` and
  `/favicon.ico`. Without the dev server, `npm run build` and then `dotnet run` serve the built app.
- **P2-4 Side by side, then cut over:** until the cutover step (step 9) the React UI lives under
  `/app/` (React Router `basename="/app"`) and the Razor pages stay unchanged, so the existing smoke
  tests stay green. At the cutover, React takes over the original page routes (P2-9) and the Razor
  catalog views are deleted.
  - Found in step 1: the ASP.NET Core app serves `/app/index.html` and `/app/assets/*`, but `/app/`
    and client routes below it return 404 (no default document or fallback). Step 4 adds a fallback
    from `/app/{**path}` to `app/index.html`.
  - **Step 4:** `MapFallbackToFile("/app/{**path:nonfile}", "app/index.html")` in `Program.cs`; `:nonfile`
    keeps a missing `/app/assets/*` file a 404. The Router's `basename` comes from Vite's `base`
    (`import.meta.env.BASE_URL`), so the cutover changes one setting. `/`, `/Catalog` and `/Catalog/Index`
    show the list; any other client route shows "Page not found." in the shell until its page exists.
    Until steps 5 and 6, "Create New", "Edit", "Details" and "Delete" are plain links to the Razor pages
    (approved 2026-11-10), which return to the Razor list after saving.
- **P2-5 Toolchain and approved npm packages:** Node.js current LTS, pinned to one exact version in
  `.nvmrc`, `package.json` `engines` and the Dockerfile's Node image tag (record the version here in
  step 1). Packages: `react`, `react-dom`, `react-router`, `typescript`, `vite`, `@vitejs/plugin-react`,
  the lint dev dependencies of Vite's `react-ts` template (ESLint and its plugins); for tests `vitest`,
  `jsdom`, `@testing-library/react`, `@testing-library/user-event`, `@testing-library/jest-dom`; for
  browser tests `@playwright/test`. Record the exact versions here in the step that adds them.
  - **Step 1** (2026-10-28): Node **26.13.0** (LTS "Lithium"; npm 11.20.0), `engine-strict` in `.npmrc`.
    Scaffolded from create-vite 9.3.3 `react-ts`, whose linter is now **oxlint** (not ESLint), so oxlint
    is the approved linter. `@testing-library/dom` is added as the required peer of
    `@testing-library/react`. Versions: react, react-dom 19.3.1; vite 8.3.5; @vitejs/plugin-react 6.1.2;
    typescript 7.0.2; oxlint 1.90.0; @types/react, @types/react-dom 19.3.0; @types/node 26.6.6;
    vitest 5.0.5; jsdom 30.1.3; @testing-library/react 16.3.3, /dom 10.4.2, /jest-dom 7.0.1.
    Not yet added: react-router, @testing-library/user-event, @playwright/test.
  - **Step 4** (2026-11-10): react-router 8.5.1; @testing-library/user-event 14.6.9. Not yet added:
    @playwright/test.
- **P2-6 Catalog JSON API** (new; PascalCase per R-7, Problem Details for errors):

  | Method and route | Result | Replaces |
  |---|---|---|
  | `GET /api/v1/catalog/items?pageSize=10&pageIndex=0` | 200, `PaginatedItemsViewModel<CatalogItem>` as is; 400 per R-16 | Index |
  | `GET /api/v1/catalog/items/{id}` | 200 item with `CatalogBrand`, `CatalogType`, `PictureUri`; 404 | Details, Edit, Delete (GET) |
  | `POST /api/v1/catalog/items` | 201 + `Location`; 400 `ValidationProblemDetails` | Create (POST) |
  | `PUT /api/v1/catalog/items/{id}` | 204; 400 (validation, or route id ≠ body `Id`); 404 | Edit (POST) |
  | `DELETE /api/v1/catalog/items/{id}` | 204; 404 (R-15) | Delete (POST) |
  | `GET /api/v1/catalog/brands`, `GET /api/v1/catalog/types` | 200, lists for the form drop-downs | `ViewBag` select lists |

  - **Overposting:** the body binds to `CatalogItem` (one set of DataAnnotations, same messages), then
    only the fields in `CatalogController.BindFields` are copied to the stored item; `PictureUri` and
    the navigation properties are ignored. Unit-tested.
  - **Validation:** the server is the authority (R-9 picture names, price, stock ranges). The React
    form shows the server's messages per field; client-side checks only mirror them for quick feedback.
  - **Logging:** the API actions keep the legacy-style "Now loading..."/"Now processing..." lines.
  - **Step 2** (`Controllers/WebApi/CatalogApiController.cs`, field copy in `Services/CatalogItemFields.cs`):
    PUT copies the fields onto the *stored* item and saves that one, because the EF service already
    tracks it after the 404 lookup (saving a second instance with the same key would throw). A route id
    that differs from the body `Id` is a plain 400 Problem Details with a `detail`, not a field error
    (MVC's ModelState keys ignore case, so `Id` would merge into the route's `id`). POST reads the item
    back, so the 201 body has the brand and type like GET; `Location` is absolute. Attribute validation
    (`[ApiController]`) runs before the action, so a request with annotation errors doesn't also get
    P2-13 errors. No global `AddProblemDetails()`: it isn't needed for `[ApiController]` results and
    would give the Razor pages' empty 400/404 responses a body (R-23).
- **P2-7 Antiforgery:** kept for all state-changing catalog API calls (the Razor forms had it).
  Built-in antiforgery with a header token (`X-XSRF-TOKEN`) and a readable `XSRF-TOKEN` cookie issued
  by the API; validated on `POST`/`PUT`/`DELETE` under `/api/v1/catalog`. Missing or wrong token: 400.
  Data Protection keys as D-H.
  - **Step 2:** `AddAntiforgery(HeaderName = "X-XSRF-TOKEN")`; the Razor forms still validate their form
    field (read when the header is absent; tested). `Infrastructure/AntiforgeryCookieFilter.cs` on the
    catalog API controller issues `XSRF-TOKEN` (not HttpOnly, `SameSite=Strict`, `Path=/`, `Secure` on
    HTTPS) with every GET, plus the HttpOnly antiforgery cookie it is paired with;
    `[AutoValidateAntiforgeryToken]` checks POST, PUT and DELETE. The client must call a GET under
    `/api/v1/catalog` before its first write (forms load brands/types or the item anyway). The cookie
    value is URL-encoded by ASP.NET Core; decode it before sending it as the header.
- **P2-13 Unknown brand or type ids** (step 2, approved 2026-10-29): POST/PUT with a `CatalogBrandId` or
  `CatalogTypeId` that doesn't exist returns 400 `ValidationProblemDetails` with errors on those fields.
  The Razor drop-downs never sent one; the services would have thrown (mock) or hit the foreign key
  (database), both 500. The Razor forms are unchanged.
- **P2-8 Footer (R-11):** a new `GET /api/v1/session` returns the machine name and session start time,
  starting the session on first call, with the same `M/d/yyyy h:mm:ss tt` (en-US) format. This is the
  one API endpoint that starts a session (R-11 said API controllers start none). The page shell is a
  static file, so `SessionStartFilter` no longer sees a page request.
  - **Step 3** (`Controllers/WebApi/SessionApiController.cs`): returns `{"MachineName":…,"SessionStartTime":…}`
    (`Models/SessionInfo.cs`, PascalCase) with `Cache-Control: no-cache,no-store`. The values are written
    by `SessionStartFilter.EnsureStarted`, now shared by the filter and the endpoint, so the format is
    unchanged. Razor pages and the API share one session cookie and show the same values in either order
    (tested). The endpoint does not issue the `XSRF-TOKEN` cookie (decided 2026-10-29; P2-7 stays limited
    to `/api/v1/catalog`).
- **P2-16 Form validation and failed saves** (step 6, approved 2026-11-10): before sending, the React
  Create and Edit forms check only what JSON needs, on submit, with the Razor UI's messages: Name
  present ("The Name field is required."), Price and the three stock fields present and numeric
  ("The Price field is required.", "The field Price must be a number.", stock fields whole numbers).
  Every other rule (price range and decimals, stock ranges, picture name R-9, brand/type P2-13) is
  checked by the server only; its 400 `ValidationProblemDetails` messages are shown at their fields
  (first message each, `field-validation-error`), others in the validation summary. The Razor UI's
  jQuery validation showed all of them before posting. A value the server cannot read (key `$.Field`,
  e.g. a stock beyond `int`) shows MVC's "The value '…' is not valid for {label}." A save that fails
  otherwise (500, network, rejected antiforgery token) stays on the form with the entered values and
  "The catalog item could not be saved." in the summary; a PUT answered 404 shows "The catalog item
  was not found." there. Edit for a missing item: as P2-15. Create shows "The catalog could not be
  loaded." if the brands or types can't be loaded. Vitest-tested.
  - **Step 6:** `components/CatalogItemForm.tsx` (both forms; markup, ids, input types and start values
    copied from the HTML the Razor pages render, including Edit's "CatalogBrandId"/"CatalogTypeId"
    labels, R-24), rules and mapping in `catalogItemForm.ts`, `pages/CatalogCreate.tsx`,
    `pages/CatalogEdit.tsx`. The body matches what the Razor forms posted: an empty description is
    `null`, Create sends `dummy.png` and no `OnReorder` (false; for Edit see P2-17). The form has `noValidate` (jQuery validation set it too). All links of the React UI
    now stay under `/app/`; it no longer links to a Razor page.
- **P2-17 `OnReorder` survives an edit** (approved 2026-11-10): the React Edit form sends the item's
  stored `OnReorder` back with the PUT, so editing no longer changes it. The Razor Edit form never
  posted the field, and its `[Bind]` list included it, so every Razor edit reset it to false (the
  Razor pages still do until the cutover). No page shows the field. Vitest-tested.
- **P2-18 Unknown client routes** (step 4; recorded 2026-11-10): a URL under `/app/` that matches no
  React route (e.g. `/app/Nope`) gets the shell with status 200 and "Page not found." in place of the
  page content, where the Razor UI returned an empty 404. Whether the server should answer such URLs
  with a real 404 belongs to the open question on status codes (cutover). Vitest-tested.
- **P2-9 Cutover routes:** `/`, `/Catalog`, `/Catalog/Index`, `/Catalog/Details/{id}`,
  `/Catalog/Create`, `/Catalog/Edit/{id}` and `/Catalog/Delete/{id}` (same query strings, e.g.
  `?pageSize=…&pageIndex=…`) are served by a SPA fallback to `app/index.html` and matched by React
  Router. The MVC catalog actions and views are removed; `/items/{id}/pic` and all API routes stay.
- **P2-10 Container:** the Dockerfile gets a Node build stage (`npm ci`, `npm run build`) before
  `dotnet publish`, so `MapStaticAssets` includes the built files. The compose build context moves
  from `./src/eShopMvc.Web` to `./src` (both projects needed). The runtime image stays without Node.
- **P2-11 Look:** same markup classes and the existing CSS (`wwwroot/css/{custom,base,site}.css`,
  Bootstrap 4.6.2 CSS, fonts, images), linked from `index.html`, not bundled by Vite. Texts, labels,
  price and pager formatting copied from the Razor UI, including the preserved quirks (R-24 labels and
  pager text, R-29 banner fix). jQuery, jQuery Validation and `bootstrap.min.js` are not used by React;
  remove them with LibMan at the cutover if nothing else needs them.
  - **Step 4:** the four style sheets are linked in the client's `index.html` by absolute path with
    `vite-ignore` (Vite leaves them alone). The banner and title keep "Catalog manager (MVC)"; the list
    sets the title "Index - Catalog manager (MVC)" (approved 2026-11-10). Prices use
    `Intl.NumberFormat('en-US', USD)` whatever the browser language (R-12).
- **P2-14 Failed API calls in the list** (step 4, approved 2026-11-10): the React list shows a message in
  place of the table: "Invalid paging values." when the API returns 400 (R-16; the Razor UI returned an
  empty 400 page), otherwise "The catalog could not be loaded.". The page URL's `pageSize`/`pageIndex`
  go to the API unchanged, so the server alone decides what is valid. Vitest-tested.
  While another page of the list loads (pager), the current one stays on screen until the answer
  arrives, as a browser kept the old Razor page until the new one came.
- **P2-15 Missing items and failed deletes** (step 5, approved 2026-11-10): the React Details and Delete
  pages show a message and a "Back to List" link in place of the item: "The catalog item was not found."
  for an unknown id and for an id that isn't a number (both API 404; the Razor UI returned empty 404
  and 400 pages), "The catalog item could not be loaded." for any other failure. The id of the page
  URL goes to the API unchanged (URL-encoded), so the server alone decides, as with P2-14 (changed
  2026-11-10; step 5 first checked for digits in the client and skipped the call).
  A failed delete stays on the Delete page with a message above the buttons: "The catalog item was not
  found." on 404 (R-15), otherwise "The catalog item could not be deleted." (includes a rejected
  antiforgery token, 400). This is what React displays; the HTTP status of the page URLs after the
  cutover is still an open question. Vitest-tested.
  - **Step 5:** `pages/CatalogDetails.tsx`, `pages/CatalogDelete.tsx`, shared `components/CatalogItemView.tsx`
    and `hooks.ts` (`useCatalogItem`, `useDocumentTitle`). Delete sends `DELETE /api/v1/catalog/items/{id}`
    with `X-XSRF-TOKEN` from the decoded `XSRF-TOKEN` cookie (issued by the item's GET, P2-7), disables
    the button meanwhile and then shows the list. The list's "Details" and "Delete" are Router links
    now; "Edit" (list and Details page) and "Create New" stay Razor links until step 6.
- **P2-12 Tests:**
  - **Smoke tests (xUnit):** new API tests for P2-6/P2-7/P2-8, skipped for `ESHOP_TARGET=legacy`.
    At the cutover the HTML-scraping Catalog tests and `SuiteFixture`'s clean-up move to the JSON API.
  - **Browser tests:** Playwright (`@playwright/test`) in `tests/eShopMvc.E2ETests/` (an npm project,
    not in `eShopMvc.slnx`), using `ESHOP_BASE_URL`, the `smoke-` name prefix and clean-up rules of the
    smoke tests. Flows: list and paging, details, create (including a validation error), edit, delete,
    footer. Chromium only unless decided otherwise.
  - **Component tests:** Vitest + React Testing Library in `src/eShopMvc.Client`.

## Open questions (ask when the step that needs them is reached)
- **Error page (R-23) after the cutover:** keep `Views/Shared/Error.cshtml` for server-side errors, or
  replace it with a static page? (Failed API calls are answered: P2-14 for the list, P2-15 for
  Details and Delete, P2-16 for the forms.)
- **Status codes on page URLs:** after the cutover, `/Catalog/Details/999` returns the SPA shell (200)
  and React shows "not found", where the Razor UI returned 404 (likewise 400 for invalid paging).
  Accept and document, or check ids on the server before serving the shell?
- **Legacy in the smoke tests:** after the cutover, keep the HTML Catalog tests for `ESHOP_TARGET=legacy`
  only, or retire them?
- **Exact Node version and Playwright browsers** (step 1 and step 7).

## Steps
1. **Client scaffold:** `src/eShopMvc.Client` from Vite's `react-ts` template, Node pinned, npm scripts
   (`dev`, `build`, `test`, `lint`), Vite proxy and `outDir` (P2-2, P2-3), `.gitignore` entries
   (`node_modules`, `wwwroot/app`). No change to the ASP.NET Core app yet.
2. **Catalog JSON API** (P2-6) with Problem Details and antiforgery (P2-7): C# unit and integration
   tests, API smoke tests.
3. **Session endpoint** for the footer (P2-8), with tests.
4. **Shell and list:** layout (header, banner, footer), CSS reuse (P2-11), Index with paging under `/app/`.
5. **Details and Delete** pages.
6. **Create and Edit** forms, with server validation messages and antiforgery.
7. **Playwright** project and browser tests against `/app/` (P2-12).
8. **Container:** Node build stage, compose build context (P2-10); smoke and browser tests against
   the container.
9. **Cutover:** SPA fallback for the original routes (P2-9), Router `basename` to `/`, Razor catalog
   views and MVC catalog actions removed, unused client libraries removed, smoke and browser tests
   moved to the original routes; answer the open questions first.
10. **Parity:** compare the React UI with the phase-1 Razor UI page by page; record the results and
    every deliberate difference (with its test) in `docs/parity.md`; update `AGENTS.md` (layout,
    commands, target stack).

## Progress
- [x] Client scaffold (Node 26.13.0; client build, 1/1 Vitest, lint clean; unit 129/129,
  integration 64/64, smoke vs new 40/0/4; `/app/index.html` served by the app, Vite proxy checked)
- [x] Catalog JSON API (unit 143/143, integration 81/81, smoke vs new 44/0/4 of 48 in database mode;
  client build, 1/1 Vitest, lint clean)
- [x] Session endpoint (unit 143/143, integration 85/85, smoke vs new 45/0/4 of 49; client build,
  1/1 Vitest, lint clean)
- [x] Shell and list (unit 143/143, integration 93/93, smoke vs new 51/0/4 of 55; client build,
  16/16 Vitest, lint clean; not yet looked at in a browser)
- [x] Details and Delete (unit 143/143, integration 93/93, smoke vs new 51/0/4 of 55; client build,
  29/29 Vitest, lint clean; not yet looked at in a browser)
- [x] Create and Edit (unit 143/143, integration 93/93, smoke vs new 51/0/4 of 55; client build,
  70/70 Vitest, lint clean; the forms' POST/PUT/DELETE bodies checked by hand against the running
  API; not yet looked at in a browser)
- [ ] Playwright tests
- [ ] Container
- [ ] Cutover
- [ ] Parity verified

Update this checklist as steps complete.
