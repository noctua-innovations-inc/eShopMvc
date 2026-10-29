# Phase 1: port to ASP.NET Core MVC on .NET 10

> **Phase one is complete.** This file is a historical record of its goals and
> decisions, moved verbatim from `AGENTS.md`. Earlier phase documents are reference only.

## Goal
Port the legacy **eShopLegacyMVC** app (ASP.NET MVC 5, .NET Framework 4.x) from
Microsoft's *Modernize existing .NET applications with Azure cloud and Windows
Containers* sample to **ASP.NET Core MVC on .NET 10 (LTS)**, keeping the same
pages, routes and behavior. Later: Linux container and Azure deployment.

## Target stack
Full assessment (packages, APIs, routes, risks with file:line): `docs/assessment.md`.
Parity verification (test results, every deliberate difference with its test, open items): `docs/parity.md`.

| Legacy | Target |
|---|---|
| .NET Framework 4.8, ASP.NET MVC 5.2.7 + Web API 2 (5.2.7), `Global.asax` | .NET 10, ASP.NET Core MVC + API controllers in one app, `Program.cs` |
| `Web.config` appSettings (`UseMockData`, `UseCustomizationData`), XDT transforms | `appsettings.json` / `appsettings.{Env}.json` + options pattern |
| EF6 6.2.0, `CreateDatabaseIfNotExists` + seed, HiLo IDs via SQL sequences | EF Core (SQL Server), migrations + startup seeding; item IDs via `UseHiLo("catalog_hilo")`, brand/type IDs IDENTITY (R-21, D6, D8) |
| LocalDB, Integrated Security | Development: LocalDB `eShopMvc.CatalogDb` (D5); compose: SQL Server 2022 container, SQL auth (R-3, step 10) |
| Autofac 6.1 (+ Mvc5 / WebApi2 integration) | Built-in `Microsoft.Extensions.DependencyInjection` |
| log4net 2.0.10 (rolling file) | `ILogger<T>`, console only (readable in Development, JSON elsewhere; L1) |
| Application Insights 2.9.1 (no key in source) | Dropped; revisit at the Azure step |
| InProc session (`Session_Start` → footer) | Session middleware |
| `HandleErrorAttribute` | `UseExceptionHandler` + Error view |
| System.Web.Optimization bundles | Static files in `wwwroot`; client libraries via LibMan, committed (R-17) |
| `BinaryFormatter` (`/api/files`) | `/api/v1/files` returns JSON; `/api/files` returns 410 (R-1) |
| packages.config + PackageReference mixed, non-SDK `.csproj` | SDK-style `.csproj`, PackageReference |
| IIS Express / Windows Container (per the book) | Kestrel / Linux container |

## Known risks
- ~~`/api/files` uses BinaryFormatter, which .NET 10 doesn't support.~~ Resolved by R-1.
- ~~The committed `Web.config` has `UseMockData=false`, contradicting the old Commands note.~~ Resolved by R-2 (database mode).
- ~~Linux is case-sensitive: `log4Net.xml`, `Content/Site.css`, `Images/` are referenced with different casing.~~
  Resolved by R-6 (lowercase `wwwroot` paths) and L1 (no log4net).
- ~~JSON for `/api/brands` becomes camelCase by default.~~ Resolved by R-7 (PascalCase).
- ~~Seed SQL scripts hard-code `USE [<db name>]` and backslash paths.~~ Resolved by D7: EF Core
  creates the sequences from the model; the scripts aren't ported.
- ~~`PictureFileName` is bindable on POST and used unchecked in a file path (path traversal).~~ Resolved by R-9.
- ~~The footer needs `Session_Start` emulation and an explicit `en-US` culture.~~ Resolved by R-11 and R-12.
- ~~`UseHttpsRedirection` logs "Failed to determine the https port" on HTTP-only runs.~~ Resolved by D-D
  (redirection removed; TLS and redirects belong to the proxy / Azure front end).
- **Data Protection keys** (found in step 10): locally solved by D-H (compose volume; keys unencrypted).
  Open for Azure: a shared, encrypted key store, so tokens and session cookies work across restarts and
  replicas (D-H).
- Remaining items for the Azure step: see `docs/parity.md`, "Step 10 (containers): outcome and open items".

## Decisions
- **R-1 `/api/files`:** contract change (.NET 10 cannot produce BinaryFormatter output).
  - New: `GET /api/v1/files` returns the `BrandDTO` list as JSON (PascalCase, per R-7).
    Plain attribute route; no versioning library unless approved.
  - Old: `GET /api/files` returns `410 Gone` with a Problem Details body naming the
    replacement and a `Link: </api/v1/files>; rel="successor-version"` header. No redirect.
- **R-2 Baseline mode:** database mode (`UseMockData=false`, LocalDB), verified 2026-10-14.
  Smoke tests and parity checks use this mode.
- **R-7 JSON casing:** keep PascalCase for all API responses (`PropertyNamingPolicy = null`).
  - **XML** (step 6): not ported. Legacy Web API also negotiated XML (DataContract, with the CLR
    namespace in it, so exact parity is impossible anyway); no consumer exists. `Accept: application/xml`
    gets JSON (ASP.NET Core default); switching to 406 is one option (`ReturnHttpNotAcceptable`).
- **R-8 `/api`:** `CatalogController2` returns 404 on the running legacy app (verified
  2026-10-14). Dead code; do not port. ASP.NET Core would discover it, so leave it out.
- **R-9 Path traversal:** fix it (validate `PictureFileName`). Deliberate deviation from parity.
  Details (step 6, `Services/PictureFiles.cs`, unit-tested):
  - **Validation (allowlist):** `[PictureFileName]` on `CatalogItem.PictureFileName` accepts only a bare
    file name of ASCII letters, digits, `-`, `_` and `.`, other than `.` and `..`; Create and Edit show
    a form validation error otherwise. Null/empty passes this rule (unchanged legacy behavior).
    All legacy picture names (`1.png`–`12.png`, `dummy.png`) pass.
  - **Path check (defense in depth):** `PicController` resolves the name under `ContentRootPath/Pics`
    and returns **404** if `Path.GetRelativePath(folder, path)` is exactly `..`, starts with `..` plus a
    directory separator, or is rooted. Names that merely start with two dots (`..backup.png`) are inside.
    On Linux `\` is not a separator, so `..\x.png` is a file name there, not a parent reference.
  - Pictures live in `src/eShopMvc.Web/Pics` (copied from legacy), outside `wwwroot`, served only via
    `/items/{id}/pic`; the csproj publishes them. MIME mapping unchanged (case-sensitive). A missing
    file still throws (500), as in legacy.
  - **Pictures folder** (step 9): direct `/Pics/...` URLs return **404**. Legacy IIS *did* serve
    `~/Pics/<file>` as static files (200, checked 2026-10-18); the step-6 assumption that it didn't was
    wrong. Deliberate: one way to serve pictures, through the validated endpoint. Tested by
    `PictureTests.PicsFolder_IsNotServedDirectly`.
- **R-12 Culture** (step 6): `UseRequestLocalization` with `en-US` (no user overrides) as the only
  culture and no request culture providers, so `Accept-Language` can't change price binding or
  formatting (legacy: fixed `<globalization culture="en-US" uiCulture="en-US" />`). Containers need
  ICU (don't enable `InvariantGlobalization`; step 10).
- **R-15 Delete POST of a missing id:** 404 (legacy: 500 in database mode, redirect in mock mode),
  consistent with GET `/Catalog/Delete/{id}`.
- **R-16 Paging:** `Index` returns 400 for `pageSize < 1`, `pageIndex < 0`, or `pageSize × pageIndex`
  overflowing `int` (legacy: 500 in all three). The hidden "Previous" link on page 0 still points to
  `pageIndex=-1`, as legacy.
- **R-23 Errors:** Development uses the developer exception page (legacy local users saw the YSOD).
  Elsewhere `UseExceptionHandler("/Error")` → `ErrorController` (any HTTP method), a generic page
  without details; the middleware logs the exception (legacy didn't). Wired in step 6 with the
  template's `Error.cshtml`; restyled from legacy's in step 7. 400/404 keep empty bodies.
  - Step 7: `Views/Shared/Error.cshtml` shows legacy's text ("Error." / "An error occurred while
    processing your request.") inside the layout (legacy's full HTML document rendered nested in it),
    plus the request ID for matching the logs. No exception details (verified in Production mode).
- **R-14 Mock quirks:** fix both; nothing observable changes except brand/type now appearing.
  - `CatalogServiceMock` is a singleton shared by all requests: every list access takes a lock.
  - Every item it returns (paging and `FindCatalogItem`) has `CatalogBrand`/`CatalogType` set,
    matching the EF service's `Include()` (R-2 baseline). Paging no longer rewrites every stored item.
  - `FindCatalogItem` still returns the stored instance; `RemoveCatalogItem` matches by reference (legacy).
- **R-17 Static libraries:** latest release within the current major versions
  (jQuery 3.x, Bootstrap 4.x, matching validation libraries). Major upgrades
  (Bootstrap 5, jQuery 4) are a separate step after parity.
  - Step 7: jQuery 3.7.1, Bootstrap 4.6.2 (`bootstrap.min.js`, no Popper, as legacy), jQuery Validation
    1.22.1, jquery-validation-unobtrusive 4.0.0 (legacy served 3.3.1, 4.3.1, 1.17.0). Managed with
    LibMan (`src/eShopMvc.Web/libman.json`, jsDelivr provider = npm versions, only the files used);
    the LibMan CLI is a local tool. The restored files in `wwwroot/lib` are **committed**, so builds and
    containers need neither network nor LibMan. Minified files are served (legacy debug: unminified).
- **R-6 Static paths:** `wwwroot/css/{custom,base,site}.css` (legacy `Content/`, `Site.css` lowercased),
  `wwwroot/fonts/`, `wwwroot/images/` (legacy `Images/`), `wwwroot/favicon.ico`. The CSS's
  `../images/…` and `../fonts/…` references match the file names exactly (checked case-sensitively).
- **R-11 Session/footer:** `AddSession` (in-memory, 20-minute idle timeout as legacy InProc) and the
  global `SessionStartFilter` (in place of `Session_Start`): on a session's first request to a
  `Controller`-based (view) controller it stores `MachineName` and the start time as
  `M/d/yyyy h:mm:ss tt` (en-US), which matches legacy's output exactly (.NET 10's ICU `G` pattern may
  use U+202F before AM/PM). API controllers start no session (legacy Web API had none). Step 10:
  machine name = container hostname, time zone usually UTC, in-memory session needs sticky sessions
  or a shared store when scaled out.
- **R-18 Modernizr and Respond:** dropped (old-IE polyfills; nothing in the CSS or views uses them).
- **R-24 UI quirks:** Edit labels showing `CatalogBrandId`/`CatalogTypeId` and the pager text
  (page size, not item count) are **preserved**. The missing picture on an invalid Edit re-render is
  **fixed** (`CatalogController` sets `PictureUri` before re-rendering).
- **R-29 Banner heading on wide windows** (found in manual testing after step 10; legacy has the same
  bug): above about 2,290 px the heading "Catalog manager (MVC)" left the banner and was drawn in white
  over "Create New" and the first table row. Cause: `.esh-header:before` (an inline block as tall as the
  banner) and `.esh-header-title` must share one line for the vertical centering; the heading grows with
  `font-size: 4vw` while Bootstrap's `.container` stops at 1140 px, so once the heading (~12 em) no longer
  fits, it wraps to a second line below the banner, and `.esh-app-hero` doesn't clip. **Fixed**, a
  deliberate deviation from legacy like the R-24 Edit image: `white-space: nowrap` on `.esh-header`
  (`wwwroot/css/site.css`). No change where it already worked. Not testable automatically; verified
  with headless Edge screenshots at 1024, 1280, 1920, 2200, 2300, 2560 and 3840 px (identical before
  and after up to 2200 px; heading inside the banner at 2300 px and above; legacy broken at 2560 px;
  container identical to local). Confirmed by the maintainer in Edge at full screen width (about
  2,560 px) on the container, 2026-10-18.
- **Views use `Html.*` helpers** as in legacy (closest HTML); tag helpers only for `<partial>` and `~/`
  resolution. Converting to tag helpers is a possible clean-up after parity.
- **Create view, R-9:** legacy's Create form has no `PictureFileName` field and its validation message
  was commented out, so a rejected path wasn't shown. The message line is enabled (input still absent).
- **R-21 HiLo** (amended in step 5): `catalog_hilo` via EF Core `UseHiLo("catalog_hilo")` instead of
  `CatalogItemHiLoGenerator`. Brand/type IDs are IDENTITY (as in legacy); their sequences are
  created but unused (D8).
- **D2 `ICatalogService`:** no longer `IDisposable`. Its only caller was `CatalogController.Dispose`,
  which isn't ported (R-15); the container owns service lifetimes, and the EF service must not
  dispose a `DbContext` it doesn't own.
- **D3 Nullability:** optional reference properties are nullable in C# (`CatalogItem.Name`,
  `Description`, `PictureFileName`, `PictureUri`, `CatalogBrand`, `CatalogType`; `CatalogBrand.Brand`;
  `CatalogType.Type`). With `<Nullable>` enabled, MVC treats non-nullable reference properties as
  implicitly `[Required]`, which would change legacy validation. `Name` keeps its explicit `[Required]`.
  - **Step 5:** because of this, the EF Core configuration must mark `Name`, `PictureFileName`,
    `CatalogBrandId` and `CatalogTypeId` (the required brand/type relationships) as required
    explicitly, plus `CatalogBrand.Brand` and `CatalogType.Type` (required, max 100), so the schema
    matches legacy (`docs/assessment.md` 4.2). Don't rely on nullability conventions.
    Done in `CatalogDBContext`; pinned by `CatalogDBContextModelTests`.
- **D4 `BrandDTO`:** top-level `eShopMvc.Web.Models.BrandDTO` (legacy: nested in `FilesController`),
  without `[Serializable]` (BinaryFormatter only). Same name and properties, so R-1's JSON is
  `[{"Id":…,"Brand":…}]`.
- **D5 Database and connection string:** LocalDB database `eShopMvc.CatalogDb`, separate from the
  legacy `Microsoft.eShopOnContainers.Services.CatalogDb` (R-22). Connection string name
  `CatalogDBContext` (as legacy), value only in `appsettings.Development.json`
  (`Server=(localdb)\MSSQLLocalDB;Database=eShopMvc.CatalogDb;Integrated Security=True`, no MARS).
  Other environments must supply `ConnectionStrings__CatalogDBContext` (container/Azure, SQL auth,
  R-3); no password is ever committed.
- **D6 Schema creation:** EF Core migrations (`src/eShopMvc.Web/Migrations`), applied with
  `MigrateAsync()` at startup (legacy: `CreateDatabaseIfNotExists` on first request). For Azure,
  switch to applying them from the pipeline (idempotent script or migration bundle).
- **D7 Sequences:** created by EF Core from the model (`HasSequence`, `UseHiLo`): bigint, START 1,
  INCREMENT 10. The legacy `.sql` scripts aren't ported, which resolves R-4 (`USE [db]`) and R-5
  (backslash paths).
- **D8 Brand/type IDs:** IDENTITY, as EF6 created them by convention (the legacy seeding fetched
  sequence values but EF6 discarded them). Seeding inserts the `PreconfiguredData` IDs under
  `IDENTITY_INSERT`, so the items' foreign keys always match (resolves R-19).
- **D9 Constraint/index names:** EF Core defaults (`PK_Catalog`, `FK_Catalog_CatalogBrand_CatalogBrandId`,
  `IX_Catalog_CatalogBrandId`, …), not EF6's (`PK_dbo.Catalog`, `FK_dbo.Catalog_dbo.CatalogBrand_CatalogBrandId`,
  `IX_CatalogBrandId`, …). An expected difference in schema comparisons.
- **D10 `UseCustomizationData`** (CSV/ZIP seeding, R-20): not ported, no option added. Revisit after
  parity if needed (it rewrites `Pics/`, which a read-only container can't do; see also R-25).
- **D11 Database tests:** `tests/eShopMvc.Web.IntegrationTests` against a dedicated database
  (`ESHOP_TEST_DB`, default LocalDB `eShopMvc.CatalogDb.Tests`), recreated with `MigrateAsync()` per
  run and dropped afterwards; each service test runs in a rolled-back transaction. Not the EF Core
  in-memory provider or SQLite (no sequences/HiLo, different SQL semantics).
- **D12 `System.Security.Cryptography.Pkcs` 10.0.13:** direct reference in `eShopMvc.Web` to override
  the transitive 9.0.11 from `Microsoft.Data.SqlClient` 6.1.7 (via EF Core SqlServer 10.0.13), which
  has advisories GHSA-846f-f764-76mx (high, .NET denial of service) and GHSA-2fmv-rv2c-59x7 (moderate).
  10.0.13 is patched. **Remove** once the SqlClient version EF Core uses depends on a patched Pkcs
  (check with `dotnet nuget why src/eShopMvc.Web/eShopMvc.Web.csproj System.Security.Cryptography.Pkcs`
  after removing it, then `dotnet list src/eShopMvc.Web package --vulnerable --include-transitive`).
- **Seeding hooks:** `CatalogDBInitializer` is registered with both `UseSeeding` and
  `UseAsyncSeeding` (shared plan), so `Migrate()` and `MigrateAsync()` seed identically; seeding
  runs only when `CatalogBrand` is empty. Both paths are covered by integration tests.
- **D13 Index views pulled into step 6:** `Views/Catalog/Index.cshtml` and `CatalogTable.cshtml` were
  ported with the controllers (rendered in the template layout until step 7), because the smoke
  suite's `SuiteFixture` reads `/Catalog/Index` before any test and fails the whole run without it.
- **D14 In-process tests:** `Microsoft.AspNetCore.Mvc.Testing` 10.0.13 in
  `eShopMvc.Web.IntegrationTests` (`Web/`), `WebApplicationFactory<Program>` in mock mode (a test pins
  that the app database is never touched); `Program.cs` ends with `public partial class Program;`.
- **Porting note, `Html.ActionLink`:** MVC 5's `ActionLink(text, action, null, new { @class = … })`
  compiles in ASP.NET Core but binds `null` to `controllerName` and the attributes to route values
  (`?class=…`). Use named arguments: `ActionLink(text, action, routeValues: null, htmlAttributes: …)`.
  Applied in all five views (Index in step 6; Create, Delete, Details, Edit in step 7).
- **L1 Log output** (step 8): console only. Development: `simple` formatter, single line, local time.
  Elsewhere: `json` formatter, UTC timestamps, for container/Azure log collection (structured fields
  stay searchable). No log file (legacy `logFiles/myapp.log`): it would need a third-party package and a
  persistent writable volume; add one only for a concrete consumer.
- **Log levels:** `Default` Information (includes the app's legacy-style "Now loading..." lines);
  `Microsoft.AspNetCore` and `Microsoft.EntityFrameworkCore` Warning (no SQL command per query);
  `Microsoft.EntityFrameworkCore.Migrations` and `Microsoft.Hosting.Lifetime` Information. Same in all
  environments; pinned by `AppSettingsTests`.
- **R-13 / L2 Per-request context:** console scopes on (`IncludeScopes`), so every request line carries
  ASP.NET Core's `TraceId`/`SpanId` (the working "activity id"; also the request ID on the error page),
  `RequestId`, `RequestPath`, plus `UserAgent` from `UserAgentLoggingScopeMiddleware` (legacy
  "requestinfo"). The query string is not logged.
- **L3 Health checks:** `/health/live` (no dependencies) and `/health/ready` (in database mode also
  `CatalogDatabaseHealthCheck`, `Database.CanConnectAsync()`, 5 s timeout). Body is only
  `Healthy`/`Unhealthy` (200/503); no session. Custom check, no package. Step 10 wires them to
  Docker/Azure.
- **R-26 / L4 Application Insights:** stays dropped; at the Azure step evaluate OpenTelemetry with the
  Azure Monitor exporter rather than the classic SDK.
- **Settings validation** (step 8): `CatalogSettings.Validate` runs before `Build()` and lists all
  problems: `Catalog:UseMockData` not a boolean, unknown `Catalog:*` keys, `UseCustomizationData` (D10),
  and in database mode a missing or unparseable connection string (never echoed). The validated value
  is what `IOptions<CatalogOptions>` returns.
- **D-A Base image** (step 10): `mcr.microsoft.com/dotnet/aspnet:10.0.13-noble` (Ubuntu 24.04; .NET 10 has
  no Debian images), build with `sdk:10.0.402-noble`. Has ICU 74 (R-12, not invariant), time zone data,
  and bash for the health check; runs as the built-in non-root `app` user (UID 1654); `/app` is not
  writable. Image 461 MB uncompressed. `noble-chiseled-extra` (smaller, no shell) is an option for Azure,
  where probes are HTTP and need no command inside the container.
- **D-B Container health check:** bash `/dev/tcp` request to `/health/ready` (Dockerfile `HEALTHCHECK`,
  no curl/wget). SQL Server: `sqlcmd -Q "SELECT 1"`; `web` starts only after `sqlserver` is healthy.
  Azure later: liveness `/health/live`, readiness `/health/ready`.
- **D-C Container environment:** `Production` in compose (JSON logs, error page, HSTS, which is a no-op
  over HTTP); overridable with `ASPNETCORE_ENVIRONMENT` in `.env`.
- **D-D HTTPS and forwarded headers:** `UseHttpsRedirection` removed (TLS and the HTTP→HTTPS redirect
  belong to the proxy / Azure front end; legacy never redirected either); `UseHsts` stays. Forwarded
  headers via the built-in `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` (no code): off in compose (no
  proxy), on in Azure so picture URLs get the public scheme (R-10). **Security:** this setting trusts
  `X-Forwarded-For`/`X-Forwarded-Proto` from any sender. Enable it only where the app is reachable
  solely through a proxy that sets those headers (e.g. Azure's front end), never where clients can
  reach it directly. Tested by `ForwardedHeadersTests`.
- **D-E Footer in containers (R-11):** compose sets `hostname: eshopmvc-web` (instead of the container
  ID) and the clock is UTC; `TZ` in `.env` switches to a local zone (.NET honors `TZ` on Linux).
- **D-F Database persistence:** named volume `sqlserver-data`; `docker compose down` keeps the database,
  `down -v` deletes it. Note: SQL Server's sequence cache makes `catalog_hilo` jump after a SQL Server
  restart (gaps in new item IDs, never duplicates), as with the legacy scripts' `CACHE`.
- **D-G Migrations in containers:** `MigrateAsync()` at startup for compose (EF Core's migration lock
  covers concurrent replicas). For Azure: apply migrations from the pipeline (idempotent script or
  bundle) with a schema-changing identity, give the app a data-only identity, and switch startup
  migration off by configuration.
- **Container secrets:** `.env` (git-ignored, `.gitignore` line 7) holds `MSSQL_SA_PASSWORD`; compose
  refuses to start without it; `.env.example` is the committed template. The compose connection
  string uses `sa` and `TrustServerCertificate=True`, both for the local container only.
- **D-H Data Protection keys in compose:** named volume `dataprotection-keys` on
  `/home/app/.aspnet/DataProtection-Keys` (the Dockerfile pre-creates it owned by `app`). It solves
  restarts locally: a form opened before `docker compose down`/`up` still submits (302); with the
  volume removed the same submission fails with 400 (verified 2026-10-18). The keys are stored
  **unencrypted**, so the "No XML encryptor configured" warning is expected in compose. Session
  *contents* still don't survive a restart: they live in the in-memory cache (R-11), as legacy InProc
  session was lost on an IIS restart. **Azure (open decision):** a shared, encrypted key store (e.g.
  Blob Storage for the keys plus Key Vault to encrypt them; needs packages such as
  `Azure.Extensions.AspNetCore.DataProtection.Blobs` and `.Keys`), plus a shared session store or
  sticky sessions if scaled out.

## Porting order
1. Assessment: inventory incompatible APIs and packages; update the table above.
2. Smoke tests against the running legacy app (catalog list, details, create, edit, delete).
3. Create `src/eShopMvc.Web` (empty ASP.NET Core MVC, .NET 10).
4. Models and mock data source.
5. Data layer (EF Core) and services; register in DI.
6. Controllers.
7. Views, layout, partials; static content into `wwwroot`.
8. Configuration and logging.
9. Run smoke tests against the new app; fix differences.
10. Dockerfile (Linux) and docker-compose with SQL Server.

## Progress
- [x] Legacy app builds and runs (baseline commit)
- [x] Assessment
- [x] Smoke tests
- [x] New project created (template defaults; smoke tests vs new app: 0/44, expected)
- [x] Models and mock data source (`Catalog:UseMockData=true`; unit tests 16/16)
- [x] Data layer (EF Core) and services (unit 39/39, integration 11/11; schema matches legacy except D9 names)
- [x] Controllers (+ Index/CatalogTable views, D13; unit 101/101, integration 37/37;
  smoke vs new 28 passed / 12 failed / 4 skipped, all 12 need step-7 views; vs legacy 40/0/4)
- [x] Views / static content (unit 107/107, integration 46/46; smoke vs new 40/0/4, vs legacy 40/0/4)
- [x] Config / logging (unit 129/129, integration 51/51; smoke vs new 40/0/4, vs legacy 40/0/4)
- [x] Parity verified (2026-10-18, `5f9a1ea` + step-9 tests; fresh-database start; unit 129/129,
  integration 61/61, smoke vs new 40/0/4, vs legacy 40/0/4; see `docs/parity.md`)
- [x] Containerized (Linux image + compose with SQL Server; smoke vs container 40/0/4; unit 129/129,
  integration 64/64; vs legacy 40/0/4; persistence across down/up verified)

Update this checklist as steps complete.
