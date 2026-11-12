# Parity: eShopLegacyMVC → eShopMvc.Web

> **Phase 2 (2026-11-10):** the Razor pages described below were replaced by a React UI. The
> comparison of the two is in [Phase 2: Razor UI → React UI](#phase-2-razor-ui--react-ui) at the end.
> Everything before that section is the phase 1 record, kept as written: test names and counts in it
> are those of 2026-10-18, and some of those tests no longer exist (the phase 2 section says what
> replaced them).

Status as of **2026-10-18**, commit **`5f9a1ea`** (Step 8) plus the step-9 changes (this document,
AGENTS.md, and integration tests only; no application code). The legacy app runs in database mode
(LocalDB, Decision R-2); the new app runs in database mode against its own LocalDB database
`eShopMvc.CatalogDb` (D5). Decision IDs (R-*, D*, L*) refer to the Decisions section of `AGENTS.md`;
the background is in `docs/assessment.md`.

## Summary

### What was compared, and how

| Check | How | Result |
|---|---|---|
| Behavior through HTTP | `tests/eShopMvc.SmokeTests` (44 tests) run against both apps. `ESHOP_TARGET` selects the expected result where a decision changes behavior | Same pass/fail on both apps (below) |
| Database schema | Catalog views (`sys.columns`, `sys.sequences`, `sys.foreign_keys`, `sys.indexes`, …) queried in both databases and diffed (step 5) | Identical except constraint/index names (D9) and the EF migration history table |
| Rendered HTML | Index, Details, Create, Edit, Delete fetched from both apps, with host, antiforgery tokens and footer values normalized, then diffed (step 7) | Same content; markup-level differences only (listed below) |
| Status codes and headers not covered by tests | Requests sent to both running apps (step 6, step 9) | Differences listed below |
| Fresh start | `eShopMvc.CatalogDb` dropped, app started with default settings (step 9) | Migration `Initial` applied; 12 items (IDs 1–12), 5 brands, 4 types; `/health/ready` 200 |
| Manual browser check | Performed by the maintainer on 2026-10-18 | Pages, styling and the footer look the same |
| Screenshot comparison | Headless Edge, top 700 px of `/` at 1024–3840 px wide, legacy vs new (R-29) | Byte-identical at 2200 and 2560 px before R-29; after R-29 identical up to 2200 px, heading fixed at 2300 px and above |

### Test results (2026-10-18; `5f9a1ea` + step-9 tests, updated after step 10)

| Suite | Target | Passed | Failed | Skipped |
|---|---|---|---|---|
| Unit (`tests/eShopMvc.Web.UnitTests`) | none | 129 | 0 | 0 |
| Integration (`tests/eShopMvc.Web.IntegrationTests`, LocalDB test database + in-process app) | none | 64 | 0 | 0 |
| Smoke | New app, database mode, `ESHOP_TARGET=new` | 40 | 0 | 4 |
| Smoke | Legacy app, `ESHOP_TARGET=legacy` | 40 | 0 | 4 |
| Smoke (step 10) | New app in the Linux container, `http://localhost:5080`, `ESHOP_TARGET=new` | 40 | 0 | 4 |

The 4 skipped smoke tests on each side are the other target's cases for R-1 and R-9
(`Files_Legacy_*`, `FilesV1_Legacy_*`, `*_Legacy_AcceptsTraversal*` on the new app; the `*_New_*`
counterparts on legacy).

## Deliberate differences

Every decision that changes what a user, client or operator can observe. Test names are
`Class.Method`; *Smoke*, *Integration* and *Unit* name the project.

| Decision | Difference (legacy → new) | Reason | Verified by |
|---|---|---|---|
| R-1 | `GET /api/files`: BinaryFormatter payload → **410 Gone**, Problem Details, `Link: </api/v1/files>; rel="successor-version"`. New `GET /api/v1/files` returns the same brands as JSON | .NET 10 can't produce BinaryFormatter output | Smoke `FilesApiTests.Files_New_Returns410ProblemDetailsWithSuccessorLink`, `FilesApiTests.FilesV1_New_ReturnsPascalCaseBrandDtoArray`; Integration `ApiTests.Files_Returns410ProblemDetailsWithSuccessorLinkAndNoRedirect`, `ApiTests.FilesV1_ReturnsPascalCaseBrandDtos` |
| R-7 (XML) | `Accept: application/xml` on `/api/brands`: XML → JSON | No consumer; DataContract XML contains the CLR namespace, so it couldn't match anyway | Integration `ApiTests.GetBrands_AcceptXml_StillReturnsJson` |
| R-8 | `/api` stays 404 (no change visible), but only because `CatalogController2` is deliberately not ported; ASP.NET Core would otherwise serve it | Dead code in legacy | Smoke `ApiRootTests.ApiRoot_Returns404`; Integration `ApiTests.ApiRoot_Returns404` |
| R-9 | `PictureFileName` that isn't a bare allowlisted file name: accepted (and `/items/{id}/pic` served e.g. `Web.config`) → **form validation error** on Create and Edit; a stored path outside `Pics` → 404 | Path traversal fix | Smoke `PictureFileNameTests.Create_New_RejectsTraversalWithValidationError`, `PictureFileNameTests.Edit_New_RejectsTraversalAndPictureIsUnchanged`; Integration `LayoutAndFormTests.CreatePost_PathAsPictureFileName_ShowsFieldValidationErrorAndCreatesNothing`, `PictureTests.Picture_StoredNameOutsidePics_Returns404`; Unit `PictureFilesTests.*` |
| R-9 (Create view) | Create form re-renders show the `PictureFileName` validation message (legacy had the line commented out) | Otherwise the R-9 rejection is invisible | Same tests as R-9 Create |
| R-9 (pictures folder) | `GET /Pics/1.png`: 200 (IIS static file) → **404**; pictures only via `/items/{id}/pic` | `Pics` is outside `wwwroot`; one way to serve pictures | Integration `PictureTests.PicsFolder_IsNotServedDirectly`, `ErrorPageTests.BadRequestAndNotFound_HaveEmptyBodies` (`/Pics/1.png`) |
| R-15 | `POST /Catalog/Delete/{id}` for a missing id: 500 (database mode) → **404** | Consistent with `GET /Catalog/Delete/{id}` | Integration `LayoutAndFormTests.DeletePost_MissingIdWithValidAntiforgeryToken_Returns404AndDeletesNothing` (end to end, valid token); Unit `CatalogControllerTests.DeleteConfirmed_MissingId_ReturnsNotFoundAndDeletesNothing` |
| R-16 | `pageSize < 1`, `pageIndex < 0`, `pageSize × pageIndex` overflow: 500 → **400** | Invalid input, not a server error | Unit `PagingTests.*`, `CatalogControllerTests.Index_InvalidPaging_ReturnsBadRequest`; Integration `CatalogPageTests.Index_InvalidPaging_Returns400` |
| R-17 | jQuery 3.3.1 → 3.7.1, Bootstrap 4.3.1 → 4.6.2, jQuery Validation 1.17.0 → 1.22.1, unobtrusive 4.0.0; minified files; URLs under `/lib/…` | Latest within the same major versions (jQuery 3.3.1 XSS advisories) | Integration `LayoutAndFormTests.Layout_EveryReferencedStylesheetScriptAndImageLoads`; Smoke `IndexAndPagingTests.Layout_AllReferencedStylesheetsScriptsAndImagesLoad` (files load). ⚠ No test pins the versions (documented gap, see below) |
| R-18 | Modernizr (2 versions) and Respond no longer loaded | Old-IE polyfills, unused | Integration `LayoutAndFormTests.Layout_LoadsNoModernizrOrRespond` |
| R-6 | Static URLs: `/Content/*.css` → `/css/*.css`, `/Scripts/*` → `/lib/*`, `/Images/*` → `/images/*` | Consistent lowercase paths for Linux | Integration `LayoutAndFormTests.Layout_EveryReferencedStylesheetScriptAndImageLoads`, `LayoutAndFormTests.CssReferencedFontsAndImages_Load` |
| R-24 (Edit image) | Invalid Edit POST re-renders **with** the picture (legacy: broken image) | Legacy bug | Integration `LayoutAndFormTests.EditPost_Invalid_ReRendersWithThePicture` |
| R-29 (banner heading) | Windows wider than about 2,290 px: the banner heading wrapped below the banner and was drawn in white over "Create New" and the first row → stays inside the banner at every width (`white-space: nowrap` on `.esh-header`) | Legacy CSS bug (centering trick breaks once the 4vw heading outgrows the 1140 px container) | ⚠ Visual layout, not testable automatically. **Verified manually** on 2026-10-18 with headless Edge screenshots at 1024, 1280, 1920, 2200, 2300, 2560 and 3840 px: pixel-identical before/after up to 2200 px, fixed at 2300 px and above, legacy broken at 2560 px, container identical to local. Confirmed by the maintainer in Edge at full screen width (about 2,560 px) on the container: heading in the banner, first row clean |
| R-23 | Unhandled error: YSOD for local users / nested `Error.cshtml` for remote → developer exception page in Development; elsewhere legacy's text inside the layout plus a request ID, status 500, exception logged | No details leaked; logs can be matched | Integration `ErrorPageTests.UnhandledException_ShowsLegacyTextAndRequestIdInsideTheLayout_WithoutDetails_AndIsLogged` (in-process, Production environment) |
| R-23 (400/404 bodies) | IIS HTML error pages (3–5 KB) → **empty bodies**; status codes unchanged | Parity of status codes only was required | Integration `ErrorPageTests.BadRequestAndNotFound_HaveEmptyBodies` (Production environment); status codes also Smoke `DetailsTests.ItemPage_WithoutId_Returns400`, `DetailsTests.ItemPage_UnknownId_Returns404` |
| Antiforgery | `POST` without a token: 500 (`HttpAntiForgeryException`) → **400** | ASP.NET Core's built-in behavior | Integration `CatalogPageTests.CreatePost_WithoutAntiforgeryToken_Returns400`; Smoke `CreateTests.Create_WithoutAntiforgeryToken_IsRejectedAndCreatesNothing` (≥ 400 on both) |
| R-14 | Mock mode only: brand/type set on Details/Edit/Delete without visiting Index first; list access is thread-safe | Legacy mock bugs; matches database mode | Unit `CatalogServiceMockTests.FindCatalogItem_Known_ReturnsItemWithBrandAndTypeWithoutPagingFirst`. ⚠ No concurrency test for the lock (documented gap, see below) |
| D6 | Database created/migrated and seeded **at startup** (legacy: on the first request) | Fail fast; EF Core migrations | Integration `CatalogDBInitializerTests.*`; step-9 fresh-start check |
| L3 | New endpoints `/health/live`, `/health/ready` | Docker/Azure probes (step 10) | Integration `HealthTests.Health_ReturnsHealthyWithoutDetailsOrSession`, `CatalogDatabaseStartupTests.HealthCheck_*` |
| Settings validation | Bad configuration stops startup with one message (legacy: `bool.Parse` exception, or silently ignored keys). Missing `UseMockData`: legacy crashed → defaults to `false` | Fail fast with a clear message | Unit `CatalogSettingsTests.*`; Integration `CatalogDatabaseStartupTests.InitializeCatalogDatabase_UnreachableServer_LogsCriticalWithoutPasswordAndRethrows` |
| R-13 / L1 / L2 | log4net rolling file `logFiles/myapp.log` → **console** (readable in Development, JSON elsewhere); every request line carries `TraceId`/`SpanId` (the activity id that never worked in legacy), `RequestId`, `RequestPath`, `UserAgent`; no query string | Containers/Azure collect stdout | Unit `UserAgentLoggingScopeMiddlewareTests.*`, `AppSettingsTests.*` (levels, scopes, formatter). ⚠ Output format checked manually (documented gap, see below) |

### Documented test gaps (deliberate)

- **R-17 library versions:** `libman.json` and the committed files in `wwwroot/lib` are the record; a
  test repeating the versions would only break on every intended update, and missing files are already
  caught by the asset-loading tests.
- **R-14 mock lock:** a concurrency test can pass without the lock (races are timing-dependent), so it
  would prove little; mock mode isn't the baseline (R-2).
- **L1 JSON output:** the formatter choice is pinned in configuration (`AppSettingsTests`); asserting
  real console output needs a separate process for little extra value; checked manually in step 8.

## Known harmless differences

- **Markup (step 7 HTML diff):** antiforgery token placed before `</form>` instead of after `<form>`;
  validation `<span>` class order (`text-danger field-validation-valid`); `int` inputs without
  `data-val-number` (they are `type="number"`); `+` in the price regex written as `&#x2B;`; attribute
  order on Edit's picture input; ` />` instead of `/>`. Visible content is identical.
- **Schema (D9):** EF Core constraint/index names (`PK_Catalog`, `FK_Catalog_CatalogBrand_CatalogBrandId`,
  `IX_Catalog_CatalogBrandId`) instead of EF6's (`PK_dbo.Catalog`, `FK_dbo.Catalog_dbo.CatalogBrand_CatalogBrandId`,
  `IX_CatalogBrandId`); `__EFMigrationsHistory` instead of `__MigrationHistory`.
- **Session cookie:** `.AspNetCore.Session` instead of `ASP.NET_SessionId`.
- **Picture responses:** served with `PhysicalFile` (adds `ETag`, `Last-Modified`, range support).
- **`HEAD /api/brands`:** 405 on legacy, 404 on the new app (no `HEAD` handler on either).

## Legacy behavior intentionally preserved

| Behavior | Verified by |
|---|---|
| A missing picture file still gives a **500** (legacy `File.ReadAllBytes` throws) | Manual (step 7 Production check) |
| Edit labels show the raw `CatalogBrandId` / `CatalogTypeId` (R-24) | Step-7 HTML diff (no difference) |
| Pager text shows the page size, not the item count; hidden "Previous" link on page 0 points to `pageIndex=-1` (R-24, R-16) | Smoke `IndexAndPagingTests.Paging_FirstPageOfSizeOne_ShowsOneRowAndPagerText` |
| PascalCase JSON on all APIs (R-7) | Smoke `BrandsApiTests.GetBrands_ReturnsPascalCaseBrandArray`; Integration `ApiTests.GetBrands_ReturnsPascalCaseSeedBrands` |
| `DELETE /api/brands/{id}` returns 200 and deletes nothing | Smoke `BrandsApiTests.DeleteBrand_Existing_Returns200ButDoesNotDelete` |
| `/api/brands/abc` → 400 | Manual (step 6) |
| Picture MIME mapping, case-sensitive; ≤ 0 → 400, unknown → 404 | Unit `PictureFilesTests.GetMimeType_MatchesLegacyMapping`; Smoke `PictureTests.*` |
| Fixed `en-US` culture for price formatting and binding (R-12) | Integration `CatalogPageTests.Index_PriceUsesEnUsRegardlessOfAcceptLanguage` |
| Footer `MachineName, M/d/yyyy h:mm:ss tt`, set once per session; APIs start no session (R-11) | Unit `SessionStartFilterTests.*`; Integration `LayoutAndFormTests.Footer_ShowsMachineNameAndLegacyFormattedSessionStart_StableWithinTheSession`, `LayoutAndFormTests.Api_DoesNotStartASession`; Smoke `IndexAndPagingTests.Layout_FooterShowsSessionInfo` |
| Create ignores a posted `Id`; item IDs from `catalog_hilo` (R-21); brand/type IDs IDENTITY (D8) | Integration `CatalogServiceTests.CreateCatalogItem_IgnoresSuppliedIdAndUsesHiLoId`; Unit `CatalogDBContextModelTests.*` |
| Routes, verbs, `[Bind]` lists, antiforgery on every POST, 400 without id / 404 for unknown id | Smoke `CreateTests.*`, `EditTests.*`, `DeleteTests.*`, `DetailsTests.*` |

## Not ported or deferred

- **`UseCustomizationData`** (CSV/ZIP seeding, D10): not ported; setting it stops startup with a message.
- **Application Insights** (R-26 / L4): dropped; evaluate OpenTelemetry with the Azure Monitor exporter at the Azure step.
- **Later phases (after parity):** Bootstrap 5 / jQuery 4 (R-17); converting views to tag helpers;
  a React or Blazor UI.

## Step 10 (containers): outcome and open items

Verified 2026-10-18 with `docker compose` (Linux engine): image `eshopmvc-web:local` from
`aspnet:10.0.13-noble`, SQL Server `2022-CU27-ubuntu-22.04`. Smoke tests against the container
(`http://localhost:5080`, `ESHOP_TARGET=new`): **40 passed, 0 failed, 4 skipped** (again after D-H), the first run on a
case-sensitive file system (R-6). Fresh start seeded 12 / 5 / 4; `docker compose down` / `up` kept the
database (same creation date, "No migrations were applied"); `down -v` started fresh.

| Item | Outcome |
|---|---|
| HTTPS redirection | **Done (D-D):** `UseHttpsRedirection` removed; TLS and redirects belong to the proxy / Azure front end. The startup warning is gone. Test `ForwardedHeadersTests.PlainHttp_IsServedWithoutRedirect` |
| SQL authentication (R-3) | **Done for compose:** `sa` login, connection string from `.env` via `ConnectionStrings__CatalogDBContext`, `TrustServerCertificate=True` for the local self-signed certificate. **Azure:** own login or managed identity, real certificate |
| ICU (R-12) | **Verified:** ICU 74 in the image, globalization not invariant; prices render `$19.50` |
| Footer (R-11) | **Done (D-E):** `eshopmvc-web, <UTC time>`; `TZ` optional. In-memory session still needs sticky sessions or a shared store when scaled out (Azure) |
| Forwarded headers (R-10) | **Ready (D-D):** `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, off in compose. Only where clients can't reach the app directly. Tests `ForwardedHeadersTests.*` |
| Health checks (L3) | **Done for compose (D-B):** image `HEALTHCHECK` on `/health/ready`; `web` waits for a healthy `sqlserver`. **Azure:** HTTP probes `/health/live`, `/health/ready` |
| Pictures (R-9) | **Verified:** 13 files in `/app/Pics`, read-only for the app user; `/Pics/1.png` 404 |
| Migrations (D6) | **Compose (D-G):** at startup. **Azure:** pipeline with a schema-changing identity; startup migration off |
| D12 | **Open:** remove the direct `System.Security.Cryptography.Pkcs` reference once EF Core's SqlClient depends on a patched version |
| **Data Protection keys** | **Compose: done (D-H):** named volume `dataprotection-keys`; a form opened before `down`/`up` still submits (302), and fails with 400 when the volume is removed. Keys are unencrypted, so the "No XML encryptor configured" warning is expected. **Azure: open:** shared, encrypted key store (e.g. Blob Storage + Key Vault; packages needed) so tokens work across restarts and replicas |
| Session across restarts | Session *contents* are lost when the app restarts (in-memory cache, R-11), as with legacy InProc session. **Azure: open** together with scale-out: shared session store (needs a package) or sticky sessions |
| Footer clock (difference) | Legacy printed the server's local time; containers print UTC unless `TZ` is set. Deployment setting, not code |

## Phase 2: Razor UI → React UI

Status as of **2026-11-10**, branch `react-ui`. The Razor UI is the last commit before the cutover,
tag **`phase2-pre-cutover`** (`b3243f8`); the React UI is the commit that adds this section. Decision
IDs P2-* refer to `docs/agents/phase-02-react.md`. The server keeps the JSON API, the pictures, the
health checks and the error page; every catalog page is now a React page at its old URL.

### What was compared, and how

Both UIs ran side by side in mock mode (same 12 items, no database): the Razor UI from a temporary
worktree of the tag on port 5072, the React UI on port 5071. One-off Playwright scripts (Chromium)
did the comparing; they are not in the repository because they need that worktree. To repeat:
`git worktree add <dir> phase2-pre-cutover`, run both with
`-- --Catalog:UseMockData=true --urls http://localhost:<port>`, and compare as described here.

| Check | How | Result |
|---|---|---|
| Pages at rest | Every rendered element (tag, classes, position and size to 0.1 px, own text, colour, background, font size and weight, opacity, display, field value) and the document title of: list, list `?pageSize=5&pageIndex=1`, list `pageIndex=2` (short last page), Details, Delete, Edit, Create; at widths 1280, 1920, 800 and 400 | **0 differences** in all 28 comparisons (43 to 202 elements each). Ignored: the footer's start time (two apps, two sessions) and the Razor Create view's empty trailing `<div>` |
| Form behaviour | 25 scenarios on Create and Edit (empty, malformed, out-of-range and unreadable values; valid save; delete; cancel): landing URL, title, messages, focused element, then every element as above | Messages, landing URLs and layout **identical** after the fixes below; focus differs in 6 scenarios (deliberate, P2-16) |
| Error URLs | 14 URLs (unknown and malformed ids, invalid paging, unknown paths, old URL forms): status code and what is on screen | Differences are exactly the decided ones (P2-9, P2-14, P2-15), listed below |
| Legacy cross-check | Full-page screenshots at 1280 px of the same six pages from the legacy app (port 5050, database mode) and the React UI, compared pixel by pixel (footer text hidden: machine and time differ) | **Pixel-identical**, all six |
| Behaviour through HTTP | Smoke tests against legacy, the new app and the container | Table below |
| Browser flows | `tests/eShopMvc.E2ETests` (Playwright): list and paging, details, create, edit, delete, shell, footer | 28 of 28 on ports 5070, 5071 (mock) and 5080 (container) |

**Found and fixed by the comparison** (all in this commit):

| Input on Create/Edit | Razor UI | React UI before | Now |
|---|---|---|---|
| Price `abc`, `1,000` | "…positive number with maximum two decimals." | "The field Price must be a number." | As Razor |
| Price `12.` | "The field Price must be a number." | Saved as 12 | As Razor |
| Price `99999999999` | "The field Price must be between 0 and 1000000." | "The catalog item could not be saved." (API 500) | As Razor; API answers 400 (P2-20) |
| Stock `99999999999` | "The field Stock must be between 0 and 10 million." | "The value '99999999999' is not valid for Stock." | As Razor |
| Stock `1.5` | "The value '1.5' is not valid for Stock." (server) | "The field Stock must be a number." | As Razor |

One run of the at-rest comparison showed the Razor reference's brand image as broken at three widths
right after that instance had started; the image answered 200 and the repeated run was clean at all
four widths. Recorded here because it was not explained.

### Test results (2026-11-10)

| Suite | Target | Passed | Failed | Skipped |
|---|---|---|---|---|
| Unit (`tests/eShopMvc.Web.UnitTests`) | none | 169 | 0 | 0 |
| Integration (`tests/eShopMvc.Web.IntegrationTests`) | none | 101 | 0 | 0 |
| Vitest (`src/eShopMvc.Client`) | none | 81 | 0 | 0 |
| Smoke | New app, database mode, port 5070 | 37 | 0 | 29 (legacy-only) |
| Smoke | Container, port 5080 | 37 | 0 | 29 (legacy-only) |
| Smoke | Legacy app, port 5050 | 40 | 0 | 26 (new-only) |
| Browser (`tests/eShopMvc.E2ETests`) | New app 5070; mock 5071; container 5080 | 28 | 0 | 0 |

The 25 HTML tests of the catalog pages (`tests/eShopMvc.SmokeTests/Catalog`) run only against legacy
since the cutover; against the new app the JSON API tests, `ReactShellTests` and the browser tests
cover the same behaviour.

### Deliberate differences (React vs Razor)

| ID | Difference | Why | Test |
|---|---|---|---|
| P2-9 | A page URL with an unknown or malformed id, or invalid paging, answers **200 with the shell**; React shows the message (Razor: empty 404 / 400). The API still answers 404 / 400 | The pages are static files; checking first would put page logic and a database call in front of them | Integration `SpaFallbackTests.*`; Smoke `ReactShellTests.PageRoutes_*`; Browser `details.spec.ts`, `list.spec.ts` |
| P2-14 | Invalid paging: "Invalid paging values." in place of the table. `?pageSize=abc` shows it too (Razor fell back to the default page size) | The page URL's values go to the API unchanged; the server alone decides | Vitest `App.test.tsx`; Browser `list.spec.ts` "says so for invalid paging values" |
| P2-15 | Unknown item on Details, Edit, Delete: "The catalog item was not found." with "Back to List"; a failed delete stays on the page with a message | Razor showed an empty error page | Vitest `CatalogItemPages.test.tsx`, `CatalogForms.test.tsx`; Browser `details.spec.ts`, `edit.spec.ts`, `delete.spec.ts` |
| P2-9 | **404 now:** `/Catalog/Details?id=1` (id in the query string), `/Catalog/{Details,Edit,Delete}` without an id (Razor: 400), extra segments (`/Catalog/Create/5`), every `/app/…` URL of the side-by-side period | Only the seven page routes are answered; no link produces the others | Integration `SpaFallbackTests.EverythingButThePageRoutes_IsNotFound`; Smoke `ReactShellTests.OtherUrls_AreNotFound` |
| P2-9 | `POST` to the Razor form URLs answers **405** | Only the API writes data | Integration `SpaFallbackTests.FormPostsOfTheRazorUi_AreNoLongerAccepted`; Smoke `ReactShellTests.RazorFormPost_IsNoLongerAccepted` |
| P2-9 | Without a client build the page routes are 404 (`dotnet run` alone shows no pages) | `dotnet build` does not run npm (P2-2); the container image builds the client | Integration `SpaFallbackTests.PageRoutes_ReturnTheShellIfBuilt_AndStartNoSession` |
| P2-16 | A failed save that is not a validation error (500, network, rejected token) stays on the form with the entered values and "The catalog item could not be saved." (Razor: error page, values lost) | No page reload in React | Vitest `CatalogForms.test.tsx` "stays on the form and says so after…" |
| P2-16 | **Focus** after a failed save: the first field with a message (Razor: the last-edited invalid field; none after a server-rendered message) | Simpler and predictable | Vitest `CatalogForms.test.tsx`; Browser `create.spec.ts` |
| P2-16 | **Timing:** a message changes or goes while its field is corrected; fields without a message are checked on save only (Razor also checked on leaving a field, and every field while typing after a failed submit) | Razor's check-on-leave moved the button from under the mouse, so a first click on "Create" could be swallowed | Vitest `CatalogForms.test.tsx` "changes or removes a message…"; Browser `create.spec.ts` |
| P2-17 | Editing an item keeps its `OnReorder` (the Razor Edit form reset it to false) | Not on the form; the stored value is sent back | Vitest `CatalogForms.test.tsx` "sends the stored OnReorder back…" |
| P2-19 | Page views in the log: the API's "Now loading…" lines carry `Page` (from the client's `X-Client-Page` header) instead of one "Now loading... /Catalog/…" line per page | Pages are static files; navigation inside the app asks the server for no page | Unit `ClientPageLoggingScopeMiddlewareTests.*`; Vitest `pageHeader.test.tsx`; Browser `shell.spec.ts` |
| P2-20 | Price above `int.MaxValue`: 400 with the range message (was 500); `1000000.4` rejected (was rounded and accepted) | Range checked as a decimal | Unit `CatalogItemValidationTests.*`; Integration `CatalogApiTests.Create_PriceAboveTheRange_*` |
| P2-7 | Antiforgery token in the `X-XSRF-TOKEN` header, from the `XSRF-TOKEN` cookie (Razor: hidden form field) | JSON requests have no form field | Integration and Smoke `CatalogApiTests.*`; Vitest `CatalogForms.test.tsx` |
| P2-8 | The footer's values come from `GET /api/v1/session`, which starts the session (Razor: any page did). Same text and format | The shell is a static file | Integration and Smoke `SessionApiTests.*`; Browser `shell.spec.ts` "Footer" |
| P2-11 | jQuery, jQuery Validation and `bootstrap.min.js` are gone; the error page's layout has no scripts | React needs none of them | Integration `StaticAssetTests.ScriptsOfTheRazorUi_AreGone`, `ErrorPageTests.*` |

### Known harmless differences

- **Markup of a validation message:** jQuery Validation wrapped the text in an inner
  `<span id="…-error">` and marked checked inputs with the class `valid`; React prints the text
  directly and adds no `valid`. No style sheet uses either; position, size and colour are identical.
- **Between pages:** Razor reloaded the whole document; React swaps the page content and keeps the
  header and footer. While a list page loads, the previous one stays on screen (P2-14).
- **Brief empty state:** the footer text and the page content appear a moment after the shell
  (API calls); Razor delivered them in the HTML.
- **`/app/index.html`** is still served as a static file and shows "Page not found." in the shell
  (P2-18). No link leads there.
- **Disabled save button:** React disables "[ Create ]"/"[ Save ]" while the request runs; Razor
  did not, so a double click could post twice.

### Preserved from the Razor UI (and through it from legacy)

Same URLs, titles, labels and texts, including the kept quirks: the pager's "Showing {page size} of
{total}" on a short last page and always-rendered Previous/Next (R-24), the Edit form's
"CatalogBrandId"/"CatalogTypeId" labels (R-24), the banner fix (R-29), en-US prices whatever the
browser language (R-12), `dummy.png` for new items, the read-only picture name on Edit, a 51-character
name accepted by the form (mock mode stores it; the database column has 50).

### Test gaps (deliberate)

- **Chromium only** (P2-12); Firefox and WebKit were not run.
- **Failed loads and saves** (500, network, rejected token) are covered by Vitest with a stubbed
  `fetch`, not in a browser: they need a faulty server.
- **Hover, active and transition styles** were not compared; only the styles of elements at rest and
  after a submit (mouse moved away).
- **The legacy cross-check** is at one width (1280 px) and at rest; legacy's form behaviour was not
  re-compared (phase 1 established Razor = legacy there).
- **The comparison scripts are not in the repository**; this section and P2-12 "Step 10" describe
  the method.

## Phase 3: HTML table → AG Grid on the list page

Phase 3 (`docs/agents/phase-03-grid.md`) replaced the list's `<table>` by an AG Grid (Community,
MIT) that fills the window and scrolls on its own. Compared with the phase 2 UI (commit `c132c54`)
by measurements and screenshots of the list at 1920×1080, 1280×800 and 1000×700 (one-off Playwright
scripts, not committed). Steps 5 to 7 (requested after a look at the grid) then removed the footer
with its session, made everything smaller and corrected the pager. The URLs and the catalog API are
unchanged; `GET /api/v1/session` is gone.

### Test results (2026-11-11, after step 7)

| Suite | Result |
|---|---|
| Client build, lint | clean; script 1,013 kB (295 kB gzip), was 274 kB (86 kB gzip) |
| Vitest | 94/94 |
| C# unit tests | 163/163 |
| C# integration tests (LocalDB) | 99/99 |
| Smoke vs new (port 5070, database mode) | 36 passed, 29 skipped (legacy-only) of 65 |
| Browser (port 5070) | 35/35 |
| Smoke vs container (port 5080, image rebuilt) | 36 passed, 29 skipped (legacy-only) of 65 |
| Browser vs container (port 5080) | 35/35 |
| `dotnet build` | 0 warnings, 0 errors |

Not rerun: the smoke tests against legacy (nothing they cover changed).

### Deliberate differences (grid vs table)

| ID | Difference | Why | Test |
|---|---|---|---|
| P3-4 | The list page is as tall as the window: header, banner, "Create New" and pager stay in place, the grid takes the rest and scrolls its rows under a fixed header row. If that leaves the grid less than 300px (windows lower than about 562px since P3-8), the grid is 300px and the page scrolls as before | The request of phase 3 | Browser `list.spec.ts` "in a window with room for the grid", "in a low window…"; Vitest `App.test.tsx` "fills the window on the list page only" |
| P3-5 | Rows, cells and headers are `div`s with the roles `grid`, `row`, `gridcell`, `columnheader`; there is no `<table>` | AG Grid's markup | Vitest `App.test.tsx`; Browser `list.spec.ts` |
| P3-5 | Text in a cell **wraps** and the row grows (the table never wrapped and grew wider than the page instead: 1466px in a 1440px column) | Columns have a width now; nothing is cut off | Looked at, not tested |
| P3-5 | Columns can be **resized** by dragging the header's edge; widths are close to the table's, name and description share the rest; too narrow a window scrolls the grid sideways, not the page | Free with the grid | Looked at, not tested |
| P3-5 | **Keyboard:** Tab stops once on the links' cell of a row before its three links (the table: the links only); the arrow keys move between cells once one has the focus | AG Grid's documented pattern for links in cells; its default never reaches the links | Browser `list.spec.ts` "reaches the links of the rows with the Tab key…" |
| P3-5 | AG Grid adds its theme as `<style>` elements to the page | Its theming API | Browser `shell.spec.ts` (linked style sheets only) |
| P3-2 | The script is 3.7 times larger (see the table above) | The grid; only the modules in use are registered | Measured at build |
| P3-7 | **No footer** on any page (React pages and the Razor error page). With it went the session: `GET /api/v1/session` (now 404), the `.AspNetCore.Session` cookie, the `TZ` setting and the container's host name. Supersedes R-11, P2-8 and D-E | "The footer has no value" (2026-11-11); the session only held the footer's two values | Browser `shell.spec.ts` "Footer: is gone from every page…"; Vitest `App.test.tsx` "has no footer…"; integration `SpaFallbackTests` (`/api/v1/session` is 404, pages set no cookie) |
| P3-8 | **Smaller sizes on all pages:** base font 14px (16px); list thumbnails 84px wide (120px, −30%); grid cell padding 6px top and bottom (12px); buttons' padding 7px/14px (16px/24px) and gap above 7px (16px); banner 72px high with a 28px title (12.5% and 4% of the window's width: 240px and 77px at 1920). Everything sized in `rem` follows the base font. One scale for text: banner title 28px, page titles (Create, Edit, Details, Delete) 21px (48px) with half the padding, the Delete question 17.5px (28px), the "Edit \| Back to List" links 14px (32px), everything else 14px | "Everything is too large" (2026-11-11) | Browser `list.spec.ts` "uses the smaller sizes of P3-8", `shell.spec.ts` "Font sizes (P3-8)" |
| P3-9 | **A page index beyond the last page shows the last page** and the URL is corrected (replaced, so Back skips the empty page); an empty catalog shows its first page. Before: an empty grid with "Page 4 - 2". Costs a second API call in that case; the API is unchanged and still answers such an index with an empty page | Confusing (reported 2026-11-11) | Vitest `paging.test.ts`, `App.test.tsx` "shows the last page…", "shows the first page of an empty catalog…"; Browser `list.spec.ts` "shows the last page for a page index beyond it…" |
| P3-9 | **The pager counts the rows on the page:** "Showing 2 of 12 products - Page 2 - 2" (Razor UI and phase 2: "Showing 10 of 12…", the page size). Supersedes this part of R-24 | Confusing (reported 2026-11-11) | Vitest `Pager.test.tsx`; Browser `list.spec.ts` "hides Next on the last page" |

### Known harmless differences

- Rows are 68px high since P3-8 (step 3: 104px; table: 103px); the header row is 48px (table: 48px).
  Measured: eleven full rows at 1920×1080 (step 3: four), seven at 1280×800, five at 1280×720; the
  page scrolls at none of them.
- The images `brand_dark.png` and `main_footer_text.png` are still in `wwwroot/images` (and in
  `StaticAssetTests`); nothing shows them since P3-7.
- An empty catalog's pager still reads "Page 1 - 0", as in the Razor UI.
- The price keeps the table's weight (300): there `.esh-table td` won over `.esh-table-price`.
- Between two pages of the list the rows change without animation, as in the table.

### Test gaps (deliberate)

- **Hover and focus styles** of the grid were not compared with the table's.
- **The smaller sizes on the Create, Details and Delete pages** were not looked at one by one (Edit
  and Details were, in screenshots); their browser tests pass.
- **An empty catalog** in a browser: the grid's "No Rows To Show" was not looked at (Vitest covers the pager).
- **Screen readers** were not tried; the grid has AG Grid's ARIA roles and its rows are in reading order.
- **The Vite dev server** (port 5173) stopped answering after the first page load while this was
  built (`ECONNRESET` from its proxy for `/lib/bootstrap/…`); the app on port 5070 was healthy. Not
  investigated; all checks ran against the built client on port 5070.