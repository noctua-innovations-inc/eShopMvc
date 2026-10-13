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
  src/                             new ASP.NET Core app (eShopMvc.Web)
  tests/                           smoke tests, runnable against either app
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

## Commands
| Task | Command (PowerShell, from repo root) |
|---|---|
| Build + run legacy app | `.\run-legacy.ps1` → http://localhost:5050 (press `Q` to stop) |
| Build new app | `dotnet build src` |
| Run new app | `dotnet run --project src/eShopMvc.Web` → http://localhost:5060 |
| Run tests | `dotnet test tests` |

The legacy app needs the VS Developer PowerShell (handled by `run-legacy.ps1`)
and runs on mock data (`UseMockData=true` in its `Web.config`).

## Target stack (proposed – confirm during assessment)
| Legacy (expected) | Target |
|---|---|
| ASP.NET MVC 5, Global.asax | ASP.NET Core MVC, `Program.cs` |
| `Web.config` appSettings / connection strings | `appsettings.json` + options pattern |
| Entity Framework 6 | EF Core (SQL Server) |
| Autofac | Built-in `Microsoft.Extensions.DependencyInjection` |
| log4net | `ILogger<T>` |
| System.Web.Optimization bundling | Static files in `wwwroot` |
| packages.config, non-SDK `.csproj` | SDK-style `.csproj`, PackageReference |
| Windows Container (per the book) | Linux container |

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
- [ ] Assessment
- [ ] Smoke tests
- [ ] New project created
- [ ] Models / data / services
- [ ] Controllers
- [ ] Views / static content
- [ ] Config / logging
- [ ] Parity verified
- [ ] Containerized

Update this checklist as steps complete.
