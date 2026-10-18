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
  tests/eShopMvc.Web.UnitTests/    unit tests for src/eShopMvc.Web (no running app needed)
  eShopMvc.slnx                    the only solution: web app + test projects
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
| Run new app | `dotnet run --project src/eShopMvc.Web` → http://localhost:5070 |
| Run new app with mock data | `dotnet run --project src/eShopMvc.Web -- --Catalog:UseMockData=true` |
| Unit tests | `dotnet test --project tests/eShopMvc.Web.UnitTests` |
| Smoke tests vs legacy (app must be running) | `dotnet test --project tests/eShopMvc.SmokeTests` |
| Smoke tests vs new app (app must be running) | `$env:ESHOP_BASE_URL='http://localhost:5070'; $env:ESHOP_TARGET='new'; dotnet test --project tests/eShopMvc.SmokeTests` |

The legacy app needs the VS Developer PowerShell (handled by `run-legacy.ps1`)
and runs in database mode (`UseMockData=false`, LocalDB; see Decisions R-2).

The new app reads `Catalog:UseMockData` (appsettings.json, default `false` per R-2; override with
`--Catalog:UseMockData=true` or `$env:Catalog__UseMockData='true'`). Until step 5, `false` makes
startup fail on purpose with a message saying database mode isn't implemented yet.

Smoke tests (`tests/eShopMvc.SmokeTests`, xUnit v3) call a running app over HTTP.
`ESHOP_BASE_URL` defaults to http://localhost:5050; `ESHOP_TARGET` (`legacy`|`new`, default
`legacy`) selects the expected result where a Decision changes behavior. Tests create and delete
their own `smoke-*` items and never modify seeded data. `global.json` opts `dotnet test` into
Microsoft.Testing.Platform (required by xUnit v3 on the .NET 10 SDK). Any new test project must use a framework that supports it (e.g. xUnit v3).
In MTP mode `dotnet test` takes no path argument; pass `--project <dir or .csproj>` (as above).
Add every new project to `eShopMvc.slnx` (`dotnet sln eShopMvc.slnx add <path>`); there is no
separate tests solution. Plain `dotnet test` at the root would also run the smoke tests, which
need a running app.

## Target stack
Full assessment (packages, APIs, routes, risks with file:line): `docs/assessment.md`.

| Legacy | Target |
|---|---|
| .NET Framework 4.8, ASP.NET MVC 5.2.7 + Web API 2 (5.2.7), `Global.asax` | .NET 10, ASP.NET Core MVC + API controllers in one app, `Program.cs` |
| `Web.config` appSettings (`UseMockData`, `UseCustomizationData`), XDT transforms | `appsettings.json` / `appsettings.{Env}.json` + options pattern |
| EF6 6.2.0, `CreateDatabaseIfNotExists` + seed, HiLo IDs via SQL sequences | EF Core (SQL Server), startup seeding, HiLo (custom or `UseHiLo`) |
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
- Seed SQL scripts hard-code `USE [<db name>]` and backslash paths.
- `PictureFileName` is bindable on POST and used unchecked in a file path (path traversal).
- The footer needs `Session_Start` emulation and an explicit `en-US` culture.

## Decisions
- **R-1 `/api/files`:** contract change (.NET 10 cannot produce BinaryFormatter output).
  - New: `GET /api/v1/files` returns the `BrandDTO` list as JSON (PascalCase, per R-7).
    Plain attribute route; no versioning library unless approved.
  - Old: `GET /api/files` returns `410 Gone` with a Problem Details body naming the
    replacement and a `Link: </api/v1/files>; rel="successor-version"` header. No redirect.
- **R-2 Baseline mode:** database mode (`UseMockData=false`, LocalDB), verified 2026-10-14.
  Smoke tests and parity checks use this mode.
- **R-7 JSON casing:** keep PascalCase for all API responses (`PropertyNamingPolicy = null`).
- **R-8 `/api`:** `CatalogController2` returns 404 on the running legacy app (verified
  2026-10-14). Dead code; do not port. ASP.NET Core would discover it, so leave it out.
- **R-9 Path traversal:** fix it (validate `PictureFileName`). Deliberate deviation from parity.
- **R-14 Mock quirks:** fix both; nothing observable changes except brand/type now appearing.
  - `CatalogServiceMock` is a singleton shared by all requests: every list access takes a lock.
  - Every item it returns (paging and `FindCatalogItem`) has `CatalogBrand`/`CatalogType` set,
    matching the EF service's `Include()` (R-2 baseline). Paging no longer rewrites every stored item.
  - `FindCatalogItem` still returns the stored instance; `RemoveCatalogItem` matches by reference (legacy).
- **R-17 Static libraries:** latest release within the current major versions
  (jQuery 3.x, Bootstrap 4.x, matching validation libraries). Major upgrades
  (Bootstrap 5, jQuery 4) are a separate step after parity.
- **R-21 HiLo:** use EF Core `UseHiLo("catalog_hilo")` instead of `CatalogItemHiLoGenerator`.
  Brand/type seed sequences are handled in seeding code.
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
- **D4 `BrandDTO`:** top-level `eShopMvc.Web.Models.BrandDTO` (legacy: nested in `FilesController`),
  without `[Serializable]` (BinaryFormatter only). Same name and properties, so R-1's JSON is
  `[{"Id":…,"Brand":…}]`.

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
- [ ] Data layer (EF Core) and services
- [ ] Controllers
- [ ] Views / static content
- [ ] Config / logging
- [ ] Parity verified
- [ ] Containerized

Update this checklist as steps complete.
