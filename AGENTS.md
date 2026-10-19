# eShopMvc – Modernization Guide for AI Agents

## Goal
Port the legacy **eShopLegacyMVC** app (ASP.NET MVC 5, .NET Framework 4.x) from
Microsoft's *Modernize existing .NET applications with Azure cloud and Windows
Containers* sample to **ASP.NET Core MVC on .NET 10 (LTS)**, keeping the same
pages, routes and behavior. Later: Linux container and Azure deployment.

## Repository layout
```
eShopMvc/
  legacy/eShopLegacyMVCSolution/   original app – READ-ONLY reference
  src/eShopMvc.Web/                new ASP.NET Core MVC app (.NET 10), http://localhost:5070
  tests/eShopMvc.SmokeTests/       HTTP smoke tests, runnable against either app
  tests/eShopMvc.Web.UnitTests/    unit tests for src/eShopMvc.Web (no running app, no database)
  tests/eShopMvc.Web.IntegrationTests/  database tests (own LocalDB test database; see D11)
  eShopMvc.slnx                    the only solution: web app + test projects
  dotnet-tools.json                local tools (dotnet-ef 10.0.13, libman 3.0.114); `dotnet tool restore`
  global.json                      dotnet test runner = Microsoft.Testing.Platform
  run-legacy.ps1                   builds and runs the legacy app
  AGENTS.md                        this file
  CLAUDE.md                        imports this file for Claude Code
```

## Ground rules
- **Never modify anything under `legacy/`.** Read it to understand behavior; port into `src/`.
- Work in **small steps**: one controller, one service, or one concern at a time.
- After every step: `dotnet build` must succeed and the smoke tests must pass.
- Propose a plan and wait for approval before multi-file changes.
- Do not add NuGet packages, change routes, or rename public types without asking.
- Preserve behavior first; improve design only when asked.
- If something in the legacy code is unclear, say so instead of guessing.
- New logic in `src/` with real decisions or edge cases (validation, paging, mappings)
  gets unit tests; thin CRUD pass-through code doesn't need them.

## Commands
| Task | Command (PowerShell, from repo root) |
|---|---|
| Build + run legacy app | `.\run-legacy.ps1` → http://localhost:5050 (press `Q` to stop) |
| Build new app + tests | `dotnet build` (root `eShopMvc.slnx`) |
| Run new app (creates/migrates and seeds `eShopMvc.CatalogDb`) | `dotnet run --project src/eShopMvc.Web` → http://localhost:5070 |
| Run new app with mock data | `dotnet run --project src/eShopMvc.Web -- --Catalog:UseMockData=true` |
| Unit tests | `dotnet test --project tests/eShopMvc.Web.UnitTests` |
| Integration tests (LocalDB) | `dotnet test --project tests/eShopMvc.Web.IntegrationTests` |
| Restore local tools (once) | `dotnet tool restore` |
| Add an EF Core migration | `dotnet ef migrations add <Name> --project src/eShopMvc.Web` |
| Drop the new app's database | `dotnet ef database drop --project src/eShopMvc.Web` |
| Update a client library (R-17) | `cd src/eShopMvc.Web; dotnet libman update <name> --to <version>` (e.g. `jquery --to 3.7.2`), then commit `libman.json` and `wwwroot/lib` |
| Re-download client libraries | `cd src/eShopMvc.Web; dotnet libman restore` (only needed if `wwwroot/lib` was deleted) |
| Health checks (app running) | `curl http://localhost:5070/health/live` (no dependencies), `curl http://localhost:5070/health/ready` (database in database mode); `Healthy` 200 or `Unhealthy` 503 |
| Show EF Core SQL commands | `$env:Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command='Information'; dotnet run --project src/eShopMvc.Web` |
| Smoke tests vs legacy (app must be running) | `dotnet test --project tests/eShopMvc.SmokeTests` |
| Smoke tests vs new app (app must be running) | `$env:ESHOP_BASE_URL='http://localhost:5070'; $env:ESHOP_TARGET='new'; dotnet test --project tests/eShopMvc.SmokeTests` |

The legacy app needs the VS Developer PowerShell (handled by `run-legacy.ps1`)
and runs in database mode (`UseMockData=false`, LocalDB; see Decisions R-2).

The new app reads `Catalog:UseMockData` (appsettings.json, default `false` per R-2; override with
`--Catalog:UseMockData=true` or `$env:Catalog__UseMockData='true'`). With `false` it needs
`ConnectionStrings:CatalogDBContext` (Development: LocalDB `eShopMvc.CatalogDb` in
appsettings.Development.json; elsewhere `$env:ConnectionStrings__CatalogDBContext`, see D5) and
runs `MigrateAsync()` plus seeding at startup. A missing connection string fails startup.
Integration tests use `$env:ESHOP_TEST_DB` (a full connection string), default LocalDB
`eShopMvc.CatalogDb.Tests`; they recreate and drop it, never touching the app's database.

Smoke tests (`tests/eShopMvc.SmokeTests`, xUnit v3) call a running app over HTTP.
`ESHOP_BASE_URL` defaults to http://localhost:5050; `ESHOP_TARGET` (`legacy`|`new`, default
`legacy`) selects the expected result where a Decision changes behavior. Tests create and delete
their own `smoke-*` items and never modify seeded data. `global.json` opts `dotnet test` into
Microsoft.Testing.Platform (required by xUnit v3 on the .NET 10 SDK). Any new test project must use a framework that supports it (e.g. xUnit v3).
In MTP mode `dotnet test` takes no path argument; pass `--project <dir or .csproj>` (as above).
Add every new project to `eShopMvc.slnx` (`dotnet sln eShopMvc.slnx add <path>`); there is no
separate tests solution. Plain `dotnet test` at the root would also run the smoke tests, which
need a running app.

## Configuration
Settings are validated before the app is built (`CatalogSettings.Validate`): misconfiguration stops
startup with one message listing every problem. A database that can't be reached or migrated at
startup is logged as Critical (server and database, never the password) and stops the app.

| Legacy (Web.config, assessment 4.4) | New app | Override (environment variable) |
|---|---|---|
| `UseMockData` | `Catalog:UseMockData` in appsettings.json, default `false`; must be `true`/`false` | `Catalog__UseMockData` |
| `UseCustomizationData` | Not ported (D10); setting it fails startup | none |
| Unknown `Catalog:*` keys | Fail startup (catches typos) | none |
| Connection string `CatalogDBContext` | `ConnectionStrings:CatalogDBContext`, only in appsettings.Development.json (D5); required and must parse in database mode | `ConnectionStrings__CatalogDBContext` |
| `ClientValidationEnabled`, `UnobtrusiveJavaScriptEnabled` | ASP.NET Core defaults (on); no setting | none |
| `webpages:Version`, `webpages:Enabled`, `httpRuntime targetFramework` | Not needed | none |
| `<globalization culture="en-US">` | Code, `UseRequestLocalization` (R-12); deliberately not configurable | none |
| `<sessionState mode="InProc">` | Code, `AddSession` (in-memory, 20 min) (R-11) | none |
| `compilation debug` / Web.Release.config | Environment: Development = developer exception page; otherwise error page + HSTS | `ASPNETCORE_ENVIRONMENT` |
| `log4Net.xml` | `Logging` section (levels, console formatter) | `Logging__LogLevel__<Category>`, `Logging__Console__FormatterName` |
| `ApplicationInsights.config` | Dropped (R-26) | none |
| *(new)* `AllowedHosts` | `*` (template default); may be restricted for Azure | `AllowedHosts` |
| *(new)* listening URL | launchSettings.json, port 5070 (Development only) | `ASPNETCORE_HTTP_PORTS` or `ASPNETCORE_URLS` |

Override rules: `__` stands for `:`; environment variables override appsettings*.json, and
command-line arguments (`--Catalog:UseMockData=true`) override both. Category names with dots
(`Logging__LogLevel__Microsoft.EntityFrameworkCore`) work in PowerShell (`$env:…`), Docker/compose
`environment:` and Azure app settings, but not with bash `export`; use `env "NAME=value" cmd` there.

## Target stack
Full assessment (packages, APIs, routes, risks with file:line): `docs/assessment.md`.

| Legacy | Target |
|---|---|
| .NET Framework 4.8, ASP.NET MVC 5.2.7 + Web API 2 (5.2.7), `Global.asax` | .NET 10, ASP.NET Core MVC + API controllers in one app, `Program.cs` |
| `Web.config` appSettings (`UseMockData`, `UseCustomizationData`), XDT transforms | `appsettings.json` / `appsettings.{Env}.json` + options pattern |
| EF6 6.2.0, `CreateDatabaseIfNotExists` + seed, HiLo IDs via SQL sequences | EF Core (SQL Server), migrations + startup seeding; item IDs via `UseHiLo("catalog_hilo")`, brand/type IDs IDENTITY (R-21, D6, D8) |
| LocalDB, Integrated Security | SQL Server container, SQL auth |
| Autofac 6.1 (+ Mvc5 / WebApi2 integration) | Built-in `Microsoft.Extensions.DependencyInjection` |
| log4net 2.0.10 (rolling file) | `ILogger<T>` |
| Application Insights 2.9.1 (no key in source) | Dropped; revisit at the Azure step |
| InProc session (`Session_Start` → footer) | Session middleware |
| `HandleErrorAttribute` | `UseExceptionHandler` + Error view |
| System.Web.Optimization bundles | Static files in `wwwroot` |
| `BinaryFormatter` (`/api/files`) | None in .NET 10; decision needed |
| packages.config + PackageReference mixed, non-SDK `.csproj` | SDK-style `.csproj`, PackageReference |
| IIS Express / Windows Container (per the book) | Kestrel / Linux container |

## Known risks
- `/api/files` uses BinaryFormatter, which .NET 10 doesn't support. Decide whether to drop it or change its format.
- The committed `Web.config` has `UseMockData=false` (EF6 + LocalDB), which contradicts the Commands note above. Confirm the baseline mode.
- Linux is case-sensitive: `log4Net.xml`, `Content/Site.css`, `Images/` are referenced with different casing.
- JSON for `/api/brands` becomes camelCase by default (legacy is PascalCase).
- ~~Seed SQL scripts hard-code `USE [<db name>]` and backslash paths.~~ Resolved by D7: EF Core
  creates the sequences from the model; the scripts aren't ported.
- `PictureFileName` is bindable on POST and used unchecked in a file path (path traversal).
- The footer needs `Session_Start` emulation and an explicit `en-US` culture.
- `UseHttpsRedirection` logs "Failed to determine the https port" on HTTP-only runs; decide TLS termination and redirection in step 10.

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
- [ ] Parity verified
- [ ] Containerized

Update this checklist as steps complete.
