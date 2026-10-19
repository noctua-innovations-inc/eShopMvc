# Parity: eShopLegacyMVC → eShopMvc.Web

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
