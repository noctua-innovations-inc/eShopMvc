# eShopMvc – Modernization Guide for AI Agents

## Goal
Modernize the **eShopLegacyMVC** app (ASP.NET MVC 5, .NET Framework 4.x) in phases.
See "Phases" below.

Target stack, reached with phase 2: a React UI (Vite, TypeScript) served by an ASP.NET Core app
(.NET 10) that provides the JSON API (`/api/v1/…`), the pictures and the health checks; EF Core on
SQL Server; one Linux container. The only Razor view left is the error page.

## Repository layout
```
eShopMvc/
  legacy/eShopLegacyMVCSolution/   original app – READ-ONLY reference
  src/eShopMvc.Web/                new ASP.NET Core app (.NET 10): JSON API, pictures, serves the React UI; http://localhost:5070
  src/eShopMvc.Client/             React client (Vite, TypeScript; phase 2): all pages; the list is an AG Grid Community grid (phase 3; never add ag-grid-enterprise); builds into src/eShopMvc.Web/wwwroot/app
  tests/eShopMvc.SmokeTests/       HTTP smoke tests, runnable against either app
  tests/eShopMvc.E2ETests/         browser tests of the React UI (Playwright, npm project; not in eShopMvc.slnx)
  tests/eShopMvc.Web.UnitTests/    unit tests for src/eShopMvc.Web (no running app, no database)
  tests/eShopMvc.Web.IntegrationTests/  database tests (own LocalDB test database; see D11)
  docs/                            assessment.md (phase 1 step 1), parity.md (legacy vs new app; "Phase 2": Razor UI vs React UI)
  docs/agents/                     phase documents (phase-01-port.md, phase-02-react.md, phase-03-grid.md); see "Current phase"
  eShopMvc.slnx                    the only solution: web app + test projects
  dotnet-tools.json                local tools (dotnet-ef 10.0.13, libman 3.0.114); `dotnet tool restore`
  global.json                      dotnet test runner = Microsoft.Testing.Platform
  run-legacy.ps1                   builds and runs the legacy app
  docker-compose.yml               new app + SQL Server 2022 on Linux (step 10), http://localhost:5080
  .env.example                     template for .env (git-ignored): MSSQL_SA_PASSWORD
  src/eShopMvc.Web/Dockerfile      multi-stage Linux image, builds the React client too (build context: src/)
  src/.dockerignore                for that build context
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
| Run new app (creates/migrates and seeds `eShopMvc.CatalogDb`) | `dotnet run --project src/eShopMvc.Web` → http://localhost:5070 (build the React client first, see below: without it the pages are 404 and only the API answers) |
| Run new app with mock data | `dotnet run --project src/eShopMvc.Web -- --Catalog:UseMockData=true` |
| Unit tests | `dotnet test --project tests/eShopMvc.Web.UnitTests` |
| Integration tests (LocalDB) | `dotnet test --project tests/eShopMvc.Web.IntegrationTests` |
| Restore local tools (once) | `dotnet tool restore` |
| Add an EF Core migration | `dotnet ef migrations add <Name> --project src/eShopMvc.Web` |
| Drop the new app's database | `dotnet ef database drop --project src/eShopMvc.Web` |
| Update a client library (R-17) | `cd src/eShopMvc.Web; dotnet libman update <name> --to <version>` (the only library left since the phase 2 cutover is `bootstrap`, CSS only), then commit `libman.json` and `wwwroot/lib` |
| Re-download client libraries | `cd src/eShopMvc.Web; dotnet libman restore` (only needed if `wwwroot/lib` was deleted) |
| Health checks (app running) | `curl http://localhost:5070/health/live` (no dependencies), `curl http://localhost:5070/health/ready` (database in database mode); `Healthy` 200 or `Unhealthy` 503 |
| Show EF Core SQL commands | `$env:Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command='Information'; dotnet run --project src/eShopMvc.Web` |
| Smoke tests vs legacy (app must be running) | `$env:ESHOP_BASE_URL='http://localhost:5050'; $env:ESHOP_TARGET='legacy'; dotnet test --project tests/eShopMvc.SmokeTests` (set both explicitly: `$env:` values from a new-app run stay set in the same session) |
| Smoke tests vs new app (app must be running, React client built first) | `$env:ESHOP_BASE_URL='http://localhost:5070'; $env:ESHOP_TARGET='new'; dotnet test --project tests/eShopMvc.SmokeTests` |
| Containers: first-time setup | `Copy-Item .env.example .env`, then set `MSSQL_SA_PASSWORD` in `.env` (never commit `.env`) |
| Containers: build and start | `docker compose up -d --build` → http://localhost:5080 (the image builds the React client itself, no local `npm run build` needed); `docker compose ps` shows both services `healthy` |
| Containers: logs | `docker compose logs -f web` (JSON, Production) |
| Containers: stop (keep database) | `docker compose down` |
| Containers: stop and delete database | `docker compose down -v` (also deletes the Data Protection keys, so forms opened before it fail once; next start migrates and seeds again) |
| Containers: query the database | `docker compose exec sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d eShopMvc.CatalogDb -Q "SELECT COUNT(*) FROM Catalog"'` |
| Smoke tests vs container | `$env:ESHOP_BASE_URL='http://localhost:5080'; $env:ESHOP_TARGET='new'; dotnet test --project tests/eShopMvc.SmokeTests` |
| React client: install packages | `cd src/eShopMvc.Client; npm ci` (needs Node 26.13.0 from `.nvmrc`; with fnm: `fnm install 26.13.0`, then `fnm use` in that folder) |
| React client: dev server (new app must be running) | `cd src/eShopMvc.Client; npm run dev` → http://localhost:5173/ (proxies the API, pictures and static files to http://localhost:5070) |
| React client: build | `cd src/eShopMvc.Client; npm run build` (type check + build into `src/eShopMvc.Web/wwwroot/app`; run before `dotnet build`/`dotnet run`, which then serve the pages at http://localhost:5070/) |
| React client: tests, lint | `cd src/eShopMvc.Client; npm test`, `npm run lint` |
| Browser tests: first-time setup | `cd tests/eShopMvc.E2ETests; npm ci; npx playwright install chromium` (same Node as the client; the browser goes to the user profile's `ms-playwright` cache) |
| Browser tests (new app must be running, React client built first) | `cd tests/eShopMvc.E2ETests; npm test` (uses `ESHOP_BASE_URL`, default http://localhost:5070; one file: `npm test -- tests/list.spec.ts`) |
| Browser tests vs container | `cd tests/eShopMvc.E2ETests; $env:ESHOP_BASE_URL='http://localhost:5080'; npm test` |
| Browser tests: type check, failure details | `cd tests/eShopMvc.E2ETests; npm run typecheck`; after a failure `npx playwright show-trace test-results/<test>/trace.zip` |

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
their own `smoke-*` items and never modify seeded data. The HTML tests of the catalog pages
(`Catalog/`) run only against legacy; the new app's pages are React (phase 2 cutover, P2-9), so there
the suite uses the JSON API and checks the page shell, and the browser tests cover the pages. `global.json` opts `dotnet test` into
Microsoft.Testing.Platform (required by xUnit v3 on the .NET 10 SDK). Any new test project must use a framework that supports it (e.g. xUnit v3).
In MTP mode `dotnet test` takes no path argument; pass `--project <dir or .csproj>` (as above).
Browser tests (`tests/eShopMvc.E2ETests`, Playwright, Chromium) drive the React UI of a running new app.
They create and delete their own `smoke-e2e-*` items (through the UI or the JSON API) and never modify
seeded data; one worker, because the paging tests read totals that other tests change.
Add every new .NET project to `eShopMvc.slnx` (`dotnet sln eShopMvc.slnx add <path>`); there is no
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
| `<sessionState mode="InProc">` | Not ported any more: the session only held the footer's values, and the footer is gone (P3-7; was R-11) | none |
| `compilation debug` / Web.Release.config | Environment: Development = developer exception page; otherwise error page + HSTS | `ASPNETCORE_ENVIRONMENT` |
| `log4Net.xml` | `Logging` section (levels, console formatter). Page views: the API's log lines carry `Page` (the React page that made the call, from the client's `X-Client-Page` header; P2-19) | `Logging__LogLevel__<Category>`, `Logging__Console__FormatterName` |
| `ApplicationInsights.config` | Dropped (R-26) | none |
| *(new)* `AllowedHosts` | `*` (template default); may be restricted for Azure | `AllowedHosts` |
| *(new)* listening URL | launchSettings.json, port 5070 (Development only); container: 8080 (image default), published as host port 5080 | `ASPNETCORE_HTTP_PORTS` or `ASPNETCORE_URLS` |
| *(new)* forwarded headers (R-10) | Off by default; on behind a proxy (D-D). **Trusts `X-Forwarded-*` from any sender**: enable only where the app is reachable solely through a proxy that sets them (e.g. Azure's front end), never where clients can reach it directly | `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` |

Override rules: `__` stands for `:`; environment variables override appsettings*.json, and
command-line arguments (`--Catalog:UseMockData=true`) override both. Category names with dots
(`Logging__LogLevel__Microsoft.EntityFrameworkCore`) work in PowerShell (`$env:…`), Docker/compose
`environment:` and Azure app settings, but not with bash `export`; use `env "NAME=value" cmd` there.

## Phases
- Phase 1 (complete): port to ASP.NET Core MVC on .NET 10.
  See `docs/agents/phase-01-port.md`.
- Phase 2 (complete, 2026-11-10): replace the Razor UI with React.
  See `docs/agents/phase-02-react.md`; results in `docs/parity.md`, "Phase 2".

- Phase 3 (complete, 2026-11-11): scrollable grid for the catalog list (AG Grid Community); footer and
  session removed, smaller sizes, paging corrections.
  See `docs/agents/phase-03-grid.md`; results in `docs/parity.md`, "Phase 3".

## Current phase
None in progress; the last one is phase 3 (`docs/agents/phase-03-grid.md`). Read it before starting any task, together with the
other two phase documents for the decisions that still apply (R-*, D*, L* in phase 1; P2-* in
phase 2; P3-* in phase 3), and keep to them; a new phase gets its own document under
`docs/agents/` and is named here.
