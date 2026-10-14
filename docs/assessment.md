# Assessment: eShopLegacyMVC → ASP.NET Core MVC on .NET 10

Assessed from source only, on 2026-10-14, at commit `4bd39ad`. Nothing under `legacy/` was modified.

**Path conventions.** Paths are relative to `legacy/eShopLegacyMVCSolution/src/eShopLegacyMVC/`
unless they start with `eShopLegacy.Utilities/`, `eShopPorted/` or `eShopLegacyMVC.sln`
(relative to `legacy/eShopLegacyMVCSolution/`) or with `legacy/`/`run-legacy.ps1` (repo root).
`file:N` means line N; `file:N-M` a range.

Statements marked *(inspection)* are derived from reading code, not from running the app.

---

## 1. Target framework, project structure, references

| Project | File | Type | Target | Notes |
|---|---|---|---|---|
| eShopLegacyMVC | `eShopLegacyMVC.csproj` | Non-SDK web application (`ProjectTypeGuids` `{349c5851…}` = Web Application, `eShopLegacyMVC.csproj:14`) | .NET Framework 4.8 (`eShopLegacyMVC.csproj:19`) | IIS Express (`:21`, `:444-448`). `MvcBuildViews=false` (`:20`), so Razor views are only compiled at runtime. |
| eShopLegacy.Utilities | `eShopLegacy.Utilities/eShopLegacy.Utilities.csproj` | Non-SDK class library | .NET Framework 4.8 (`eShopLegacy.Utilities.csproj:12`) | No NuGet packages. One file of code: `Serializing.cs` (BinaryFormatter). |
| eShopPorted | `eShopPorted/eShopPorted.csproj` | SDK-style web (`Microsoft.NET.Sdk.Web`) | `net48` (`eShopPorted.csproj:3`) | **Third project in the .sln** (`eShopLegacyMVC.sln:8`). It's an earlier partial port (ASP.NET Core 2.2 on .NET Framework). It was out of scope for this assessment and wasn't used as a source. |

**Project references:** `eShopLegacyMVC → eShopLegacy.Utilities` (`eShopLegacyMVC.csproj:565-568`); `eShopPorted → eShopLegacy.Utilities` (`eShopPorted.csproj:31`).

**Framework assemblies referenced by the web project** (`eShopLegacyMVC.csproj:89-229`): `System.Web*`
(Abstractions, ApplicationServices, DynamicData, Entity, Routing, Services), `System.EnterpriseServices`, `System.Management`,
`System.Runtime.Caching`, `System.Drawing`, `System.Configuration`, `System.Net.Http.WebRequest`, `System.IO.Compression.FileSystem`.
None of `System.Management`, `System.Runtime.Caching`, `System.Drawing` or `System.EnterpriseServices` is used by any `.cs` or `.cshtml` file.

**Legacy folder layout of the web project:** `App_Start/`, `Controllers/` (MVC), `Controllers/Api/` (MVC), `Controllers/WebApi/` (Web API 2),
`Models/`, `Models/Infrastructure/` (EF initializer, seed data, SQL sequence scripts), `Modules/` (Autofac), `Services/`, `ViewModel/`,
`Views/`, `Content/` (CSS), `Scripts/` (JS), `fonts/`, `Images/`, `Pics/` (product images), `Setup/` (CSV/ZIP seed data), `logFiles/` (runtime output, git-ignored).

---

## 2. NuGet packages

### 2.1 How packages are actually restored

The web project declares packages **twice**: in `packages.config` *and* as `<PackageReference>` items in the `.csproj`. Restore used the
**PackageReference** graph:

- `obj/project.assets.json` exists and targets `net48`.
- There is no `legacy/eShopLegacyMVCSolution/packages/` folder, so every `<Reference HintPath="..\..\packages\…">` (e.g. `eShopLegacyMVC.csproj:56-58`, `:59-61`, `:200-221`) and every conditional `<Import>` of `..\..\packages\…` (`:3-4`, `:458-462`) points at nothing. The `Exists()` conditions silently skip those imports.
- `run-legacy.ps1:8` passes `-p:RestorePackagesConfig=true`, but the assets file shows the PackageReference graph won.

The **resolved** versions below come from `obj/project.assets.json`.

### 2.2 Package inventory and disposition

Legend: **Built in** = provided by .NET 10 / ASP.NET Core shared framework. **Replace** = needs a different package or API.
**wwwroot** = copy the static files to `wwwroot/lib`. **Drop** = not needed.

| Package | packages.config | PackageReference (resolved) | Used by | Disposition for .NET 10 |
|---|---|---|---|---|
| Antlr | 3.5.0.2 (`:3`) | 3.5.0.2 (`.csproj:236`) | WebGrease (bundling) | Drop |
| Autofac | **4.9.1** (`:4`) | **6.1.0** (`.csproj:53`) | `Global.asax.cs`, `Modules/ApplicationModule.cs` | Replace → built-in `Microsoft.Extensions.DependencyInjection` |
| Autofac.Mvc5 | 4.0.2 (`:5`) | 4.0.2 (`.csproj:470`) | `Global.asax.cs:65,74` | Drop (built-in DI) |
| Autofac.WebApi2 | — | 6.0.1 (`.csproj:62`) | `Global.asax.cs:66,77` | Drop (built-in DI) |
| bootstrap | 4.3.1 (`:6`) | 4.3.1 (`.csproj:475`) | `_Layout.cshtml` via bundles | wwwroot (files on disk are 4.3.1: `Content/bootstrap.css`, `Scripts/bootstrap.js` headers) |
| EntityFramework | 6.2.0 (`:7`) | 6.2.0 (`.csproj:65`) | Models, Services, Global | Replace → `Microsoft.EntityFrameworkCore.SqlServer` (AGENTS.md target). EF 6.5 also runs on modern .NET, but it isn't the chosen target. |
| jQuery | 3.5.0 (`:8`) | 3.5.0 (`.csproj:480`) | bundles | wwwroot. **Files on disk are 3.3.1** (`Scripts/jquery-3.3.1.js`); see R-17 |
| jQuery.Validation | 1.19.4 (`:9`) | 1.19.4 (`.csproj:485`) | `jqueryval` bundle | wwwroot. **File on disk is v1.17.0** (`Scripts/jquery.validate.js` header) |
| log4net | 2.0.10 (`:10`) | 2.0.10 (`.csproj:239`) | Global, Controllers | Replace → `ILogger<T>`. NuGet warns NU1902 (moderate vulnerability) |
| Microsoft.ApplicationInsights | 2.9.1 (`:11`) | 2.9.1 (`.csproj:242`) | `ApplicationInsights.config`, Web.config modules | Drop for now. Revisit at the Azure step (`Microsoft.ApplicationInsights.AspNetCore` or Azure Monitor OpenTelemetry) |
| Microsoft.ApplicationInsights.Agent.Intercept | 2.4.0 (`:12`) | 2.4.0 (`.csproj:490`) | AI | Drop |
| Microsoft.ApplicationInsights.DependencyCollector | 2.9.1 (`:13`) | 2.9.1 (`.csproj:495`) | AI | Drop |
| Microsoft.ApplicationInsights.PerfCounterCollector | 2.9.1 (`:14`) | 2.9.1 (`.csproj:500`) | AI | Drop |
| Microsoft.ApplicationInsights.Web | 2.9.1 (`:15`) | 2.9.1 (`.csproj:505`) | `Web.config:36,90` | Drop |
| Microsoft.ApplicationInsights.WindowsServer | 2.9.1 (`:16`) | 2.9.1 (`.csproj:510`) | AI | Drop |
| Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel | 2.9.1 (`:17`) | 2.9.1 (`.csproj:515`) | AI | Drop |
| Microsoft.AspNet.Mvc | 5.2.7 (`:18`) | 5.2.7 (`.csproj:520`) | everything MVC | Built in (ASP.NET Core MVC) |
| Microsoft.AspNet.Razor | 3.2.7 (`:19`) | 3.2.7 (`.csproj:525`) | views | Built in |
| Microsoft.AspNet.SessionState.SessionStateModule | 1.1.0 (`:20`) | 1.1.0 (`.csproj:245`) | `Web.config:91-92` | Built in (`Microsoft.AspNetCore.Session` middleware) |
| Microsoft.AspNet.TelemetryCorrelation | 1.0.5 (`:21`) | 1.0.5 (`.csproj:248`) | `Web.config:35,88` | Built in (`System.Diagnostics.Activity` / W3C trace context) |
| Microsoft.AspNet.Web.Optimization | 1.1.3 (`:22`) | 1.1.3 (`.csproj:530`) | `App_Start/BundleConfig.cs`, views | Drop. Use static files in wwwroot with plain `<link>`/`<script>` |
| Microsoft.AspNet.WebPages | 3.2.7 (`:23`) | 3.2.7 (`.csproj:535`) | Razor host | Built in |
| Microsoft.AspNet.WebApi | — | 5.2.7 (`.csproj:90`) | `Controllers/WebApi/*` | Built in (ASP.NET Core controllers) |
| Microsoft.AspNet.WebApi.Client | — | 5.2.7 (`.csproj:93`) | transitive of WebApi.Core | Drop |
| Microsoft.AspNet.WebApi.Core | — | 5.2.7 (`.csproj:96`) | Web API | Built in |
| Microsoft.AspNet.WebApi.WebHost | — | 5.2.7 (`.csproj:99`) | Web API on IIS | Built in |
| Microsoft.CodeDom.Providers.DotNetCompilerPlatform | 2.0.1 (`:24`) | 2.0.1 (`.csproj:251`) | runtime view compilation (`Web.config:113-118`) | Drop (Razor compiled at build) |
| Microsoft.jQuery.Unobtrusive.Validation | 3.2.11 (`:25`) | 3.2.11 (`.csproj:540`) | `jqueryval` bundle | wwwroot (file on disk is v3.2.11) |
| Microsoft.Net.Compilers | 2.10.0 (`:26`, devDependency) | 2.10.0 (`.csproj:545`) | build | Drop (SDK compiler) |
| Microsoft.Web.Infrastructure | 1.0.0.0 (`:27`) | 1.0.0 (`.csproj:254`) | System.Web plumbing | Drop |
| Modernizr | 2.8.3 (`:28`) | 2.8.3 (`.csproj:550`) | `modernizr` bundle (`_Layout.cshtml:8`) | Drop, or wwwroot for strict parity. No CSS or JS in the app references Modernizr classes (no `.no-js`/`html.js` selectors in `Content/*.css`) |
| Newtonsoft.Json | 12.0.1 (`:29`) | 12.0.1 (`.csproj:102`) | Web API JSON formatter (no direct use in code) | Drop (System.Text.Json), but see R-7 on property casing. NuGet warns NU1903 (high vulnerability) |
| Pipelines.Sockets.Unofficial | 1.0.7 (`:30`) | 1.0.7 (`.csproj:108`) | **nothing** (no reference in any `.cs`) | Drop. Why it was added can't be determined from source |
| popper.js | 1.14.3 (`:31`) | 1.14.3 (`.csproj:555`) | **not in any bundle** | Drop (no view uses Bootstrap components that need Popper), or wwwroot for completeness |
| Respond | 1.4.2 (`:32`) | 1.4.2 (`.csproj:560`) | `bootstrap` bundle (`BundleConfig.cs:23`) | Drop (IE8 media-query polyfill) |
| System.Buffers | 4.4.0 (`:33`) | 4.4.0 (`.csproj:115`) | transitive | Built in |
| System.Diagnostics.DiagnosticSource | 4.5.1 (`:34`) | 4.5.1 (`.csproj:122`) | AI, Autofac | Built in |
| System.Diagnostics.PerformanceCounter | 4.5.0 (`:35`) | 4.5.0 (`.csproj:128`) | AI perf counters | Drop (Windows-only) |
| System.IO.Compression | 4.3.0 (`:36`) | 4.3.0 (`.csproj:141`) | `CatalogDBInitializer.cs:353` | Built in |
| System.IO.Compression.ZipFile | 4.3.0 (`:37`) | 4.3.0 (`.csproj:144`) | `CatalogDBInitializer.cs:353` | Built in |
| System.IO.Pipelines | 4.5.1 (`:38`) | 4.5.1 (`.csproj:152`) | transitive | Built in |
| System.Memory | 4.5.1 (`:39`) | 4.5.1 (`.csproj:159`) | transitive | Built in |
| System.Net.Http | — | 4.3.4 (`.csproj:166`) | Web API | Built in |
| System.Numerics.Vectors | 4.4.0 (`:40`) | 4.4.0 (`.csproj:169`) | transitive | Built in |
| System.Runtime.CompilerServices.Unsafe | 4.5.0 (`:41`) | 4.5.0 (`.csproj:176`) | transitive | Built in |
| System.Threading.Channels | 4.5.0 (`:42`) | 4.5.0 (`.csproj:182`) | transitive | Built in |
| System.Threading.Tasks.Extensions | 4.5.1 (`:43`) | 4.5.1 (`.csproj:188`) | transitive | Built in |
| WebGrease | 1.6.0 (`:44`) | 1.6.0 (`.csproj:230`) | bundling/minification | Drop |

Transitive-only packages in the resolved graph: `Microsoft.Bcl.AsyncInterfaces 1.1.0` (from Autofac 6), `System.IO 4.3.0`, `System.Runtime 4.3.0`,
`System.Security.Cryptography.{Algorithms,Encoding,Primitives,X509Certificates} 4.3.0` (from System.Net.Http 4.3.4). All are built in or unneeded on .NET 10.

**Net new packages the port needs:** `Microsoft.EntityFrameworkCore.SqlServer` (plus `.Design`/`.Tools` only if migrations are chosen). Everything else is in the shared framework.
AGENTS.md requires approval before adding packages.

### 2.3 Conflicts between the two package sources

| Issue | Evidence |
|---|---|
| **Autofac 4.9.1 vs 6.1.0.** packages.config says 4.9.1; PackageReference says 6.1.0, and 6.1.0 is what restores. | `packages.config:4` vs `eShopLegacyMVC.csproj:53-55`. A binding redirect forces 6.1.0 at runtime: `Web.config:54-57` |
| **Autofac.Mvc5 4.0.2 requires Autofac < 5.0.0** but 6.1.0 is resolved (NU1608). It works only because of the binding redirect. | `obj/project.assets.json` log NU1608; `eShopLegacyMVC.csproj:470-472` |
| **Packages only in PackageReference:** Autofac.WebApi2 6.0.1, Microsoft.AspNet.WebApi / .Client / .Core / .WebHost 5.2.7, System.Net.Http 4.3.4. A packages.config restore would miss Web API entirely. | `eShopLegacyMVC.csproj:62,90-101,166` |
| **Package downgrades (NU1605):** System.Diagnostics.DiagnosticSource 4.7.1→4.5.1 and System.Threading.Tasks.Extensions 4.5.2→4.5.1, both caused by Autofac 6.1.0's dependencies. | `obj/project.assets.json` logs |
| **Known-vulnerable versions:** log4net 2.0.10 (NU1902), Newtonsoft.Json 12.0.1 (NU1903). Both disappear with the port. | `obj/project.assets.json` logs |
| **Dead HintPaths / imports** to `..\..\packages\…` (folder doesn't exist). | `eShopLegacyMVC.csproj:3-4,56-233,458-462` |
| `Views/Web.config` references System.Web.Mvc **5.2.3.0**; the package is 5.2.7. Bridged by a redirect. | `Views/Web.config:12,39`; `Web.config:74-77` |
| Every package in packages.config also appears as a PackageReference with the same version, except Autofac. | (cross-check of both lists) |

---

## 3. APIs that don't exist in ASP.NET Core / .NET 10, by file

Only APIs that are absent or behave differently are listed. APIs that port unchanged are omitted: data annotations, `SelectList`,
`[ValidateAntiForgeryToken]`, `[HttpPost]`, `[Route(Name=…)]`, `Controller.File(byte[], string)`, `Html.ActionLink/EditorFor/LabelFor/DisplayFor/BeginForm/AntiForgeryToken`.

| File | API(s) | .NET 10 replacement |
|---|---|---|
| `Global.asax`, `Global.asax.cs:21` | `HttpApplication`, `Application_Start`, `Application_BeginRequest`, `Session_Start` | `Program.cs` and middleware. **No `Session_Start` event**: emulate it on first session access (see R-11) |
| `Global.asax.cs:30,78` | `GlobalConfiguration.Configure/Configuration` (Web API) | Single MVC pipeline; `builder.Services.AddControllersWithViews()` |
| `Global.asax.cs:31-34` | `AreaRegistration`, `GlobalFilters`, `RouteTable.Routes`, `BundleTable.Bundles` | `MvcOptions.Filters`, `app.MapControllerRoute`, wwwroot |
| `Global.asax.cs:43-44,112` | `HttpContext.Current` (+ `.Session`, `.Request.RawUrl`, `.Request.UserAgent`) | `IHttpContextAccessor` / `HttpContext` in middleware; `Request.Path + QueryString`, `Request.Headers.UserAgent` |
| `Global.asax.cs:50,52` | `log4net.LogicalThreadContext` | `ILogger.BeginScope` |
| `Global.asax.cs:68,85` | `System.Configuration.ConfigurationManager.AppSettings` | `IConfiguration` / options |
| `Global.asax.cs:74,77` | `DependencyResolver.SetResolver`, `AutofacDependencyResolver`, `AutofacWebApiDependencyResolver` | Built-in DI |
| `Global.asax.cs:89` | EF6 `Database.SetInitializer` | Startup code (`EnsureCreated`/`Migrate` + seed) |
| `Global.asax.cs:99-104` | `Trace.CorrelationManager.ActivityId` | *Exists* in .NET 10 but ASP.NET Core doesn't set it. Use `Activity.Current` / `HttpContext.TraceIdentifier` |
| `App_Start/RouteConfig.cs:10-17` | `RouteCollection`, `MapMvcAttributeRoutes`, `IgnoreRoute("{resource}.axd/…")`, `MapRoute`, `UrlParameter.Optional` | `app.MapControllers()` + `app.MapControllerRoute("default", "{controller=Catalog}/{action=Index}/{id?}")`. `.axd` ignore is irrelevant |
| `App_Start/WebApiConfig.cs:7-17` | `HttpConfiguration`, `MapHttpAttributeRoutes`, `MapHttpRoute`, `RouteParameter.Optional` | Explicit `[Route("api/[controller]")]` on API controllers, or a conventional route `api/{controller}/{id?}` |
| `App_Start/BundleConfig.cs` (whole file) | `System.Web.Optimization` (`ScriptBundle`, `StyleBundle`, `{version}`/`*` wildcards) | Static `<script>`/`<link>` in `_Layout.cshtml`, optionally `asp-append-version` |
| `App_Start/FilterConfig.cs:10` | `System.Web.Mvc.HandleErrorAttribute` | `app.UseExceptionHandler(...)` + an `Error` action/view |
| `Controllers/CatalogController.cs:10` | `System.Web.Mvc.Controller`, `ActionResult` | `Microsoft.AspNetCore.Mvc.Controller`, `IActionResult` |
| `Controllers/CatalogController.cs:36,82,119` | `new HttpStatusCodeResult(HttpStatusCode.BadRequest)` | `BadRequest()` / `StatusCode(400)` |
| `Controllers/CatalogController.cs:41,87,124` | `HttpNotFound()` | `NotFound()` |
| `Controllers/CatalogController.cs:62,100` | `[Bind(Include = "…")]` | `[Bind("…")]` |
| `Controllers/CatalogController.cs:142-150` | `Dispose(bool)` override disposing an injected service | Remove: in ASP.NET Core the container owns the service's lifetime (see R-15) |
| `Controllers/CatalogController.cs:162` | `Url.RouteUrl(name, values, protocol)` with `Request.Url.Scheme` | `Url.RouteUrl(name, values, Request.Scheme)` (still exists); `Request.Url` doesn't |
| `Controllers/PicController.cs:31,49` | `HttpStatusCodeResult`, `HttpNotFound` | `BadRequest()`, `NotFound()` |
| `Controllers/PicController.cs:38` | `Server.MapPath("~/Pics")` | `IWebHostEnvironment.ContentRootPath` (or `WebRootPath`) + `Path.Combine` |
| `Controllers/Api/CatalogController.cs:11` | MVC 5 `Json(object)` on a GET without `JsonRequestBehavior.AllowGet` | `Json(object)` / `Ok(object)`. Behavior differs (see R-8) |
| `Controllers/WebApi/BrandsController.cs:7` | `using System.Runtime.Remoting.Messaging;` (unused, but the namespace doesn't exist → compile error) | Delete |
| `Controllers/WebApi/BrandsController.cs:12` | `System.Web.Http.ApiController` | `ControllerBase` + `[ApiController]` |
| `Controllers/WebApi/BrandsController.cs:29,40` | `IHttpActionResult`, `Ok()`, `NotFound()` | `IActionResult` / `ActionResult<T>` |
| `Controllers/WebApi/BrandsController.cs:45,49` | `ResponseMessage(new HttpResponseMessage(...))` | `NotFound()`, `Ok()` |
| `Controllers/WebApi/FilesController.cs:11,21,30-32` | `ApiController`, returning `HttpResponseMessage` with `StreamContent` | `File(stream, contentType)`. ASP.NET Core would *serialize* an `HttpResponseMessage` as JSON instead of sending it |
| `Controllers/WebApi/FilesController.cs:31-32` + `eShopLegacy.Utilities/Serializing.cs:11-12,19-21` | `BinaryFormatter.Serialize/Deserialize` | **Removed in .NET 9+** (the API always throws `PlatformNotSupportedException`). No drop-in equivalent (see R-1) |
| `Models/CatalogBrand.cs:4`, `Models/CatalogType.cs:4`, `Models/CatalogItemHiLoGenerator.cs:5`, `Models/Infrastructure/PreconfiguredData.cs:4`, `Models/Infrastructure/CatalogDBInitializer.cs:10`, `App_Start/FilterConfig.cs:1` | `using System.Web;` (unused) | Delete |
| `Models/CatalogDBContext.cs:3-4,10` | EF6 `DbContext("name=CatalogDBContext")` (reads Web.config connection string) | EF Core `DbContext(DbContextOptions<…>)` + `AddDbContext` + `UseSqlServer(config.GetConnectionString(...))` |
| `Models/CatalogDBContext.cs:20-86` | `DbModelBuilder`, `EntityTypeConfiguration<T>`, `HasRequired().WithMany().HasForeignKey()`, `HasDatabaseGeneratedOption(None)` | `ModelBuilder`, `EntityTypeBuilder<T>`, `HasOne().WithMany().HasForeignKey().IsRequired()`, `ValueGeneratedNever()` |
| `Models/CatalogItemHiLoGenerator.cs:22` | EF6 `Database.SqlQuery<Int64>(string)` | EF Core `Database.SqlQuery<long>($"…")` (needs a `Value` column alias), or replace the class with EF Core's built-in `UseHiLo("catalog_hilo")` |
| `Models/Infrastructure/CatalogDBInitializer.cs:15,32` | `CreateDatabaseIfNotExists<T>`, `Seed` | `Database.EnsureCreated()` (or migrations) + explicit seeding at startup |
| `Models/Infrastructure/CatalogDBInitializer.cs:29` | `ConfigurationManager.AppSettings` | `IConfiguration` / options |
| `Models/Infrastructure/CatalogDBInitializer.cs:97,133,169,345` | `System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath` | `IWebHostEnvironment.ContentRootPath` |
| `Models/Infrastructure/CatalogDBInitializer.cs:328` | EF6 `Database.SqlQuery<Int64>` | as above |
| `Models/Infrastructure/CatalogDBInitializer.cs:336` | EF6 `Database.ExecuteSqlCommand` | `Database.ExecuteSqlRaw` |
| `Modules/ApplicationModule.cs:8-39` | Autofac `Module`, `RegisterType().As().SingleInstance()/InstancePerLifetimeScope()` | `services.AddSingleton/AddScoped` |
| `Services/CatalogService.cs:3,26-27,39,60` | `System.Data.Entity` `Include(lambda)`, `db.Entry().State = EntityState.Modified` | Same names in `Microsoft.EntityFrameworkCore` |
| `Views/Web.config` (whole file) | Razor host config, view namespaces, `BlockViewHandler` | `Views/_ViewImports.cshtml` (`@using`, `@addTagHelper`). Blocking direct view access is automatic |
| `Views/Shared/_Layout.cshtml:7-8,43-44` | `Styles.Render`, `Scripts.Render` | `<link>`/`<script>` tags |
| `Views/Shared/_Layout.cshtml:35` | `HttpContext.Current.Session[...]` | `Context.Session.GetString(...)` (session stores strings/bytes only) |
| `Views/Shared/_Layout.cshtml:14,30,33` | `src="~/images/…"` | Works only if tag helpers are enabled (`@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`), and the path is case-sensitive on Linux (see R-6) |
| `Views/Catalog/Index.cshtml:12` | `Html.Partial` (sync) | Exists but analyzer warns; use `<partial name="CatalogTable" model="Model.Data" />` |
| `Views/Catalog/Create.cshtml:103`, `Views/Catalog/Edit.cshtml:104` | `Scripts.Render("~/bundles/jqueryval")` | `<script>` tags (or `_ValidationScriptsPartial`) |
| `Web.config`, `Web.Debug.config`, `Web.Release.config` | XML config + XDT transforms, `system.web`, `system.webServer`, binding redirects, `system.codedom` | `appsettings.json` + `appsettings.{Environment}.json`. The rest is not needed |
| `ApplicationInsights.config` | Classic AI SDK config | Drop (Azure step) |
| `log4Net.xml`, `Properties/AssemblyInfo.cs:37` | `[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.xml")]` | `Logging` section in appsettings.json |

---

## 4. How the app works today

### 4.1 Dependency injection

- Autofac container built in `Global.asax.cs:60-81`: `RegisterControllers` and `RegisterApiControllers` (assembly scan, `:65-66`), then `ApplicationModule` (`:69`).
  It's installed as both the MVC resolver (`:74`) and the Web API resolver (`:77-78`).
- Registrations (`Modules/ApplicationModule.cs:16-39`):

| Service | Implementation | Autofac lifetime | ASP.NET Core equivalent |
|---|---|---|---|
| `ICatalogService` (mock mode) | `CatalogServiceMock` | `SingleInstance` (`:20-22`) | `AddSingleton` |
| `ICatalogService` (DB mode) | `CatalogService` | `InstancePerLifetimeScope` = per request (`:26-28`) | `AddScoped` |
| `CatalogDBContext` | itself | `InstancePerLifetimeScope` (`:31-32`) | `AddDbContext` (scoped) |
| `CatalogDBInitializer` | itself | `InstancePerLifetimeScope` (`:34-35`), but resolved once from the **root** container in `Global.asax.cs:89`, so it's effectively a singleton | Startup seeding code |
| `CatalogItemHiLoGenerator` | itself | `SingleInstance` (`:37-38`) | `AddSingleton` (or EF Core `UseHiLo`) |

- The mode switch is `UseMockData` (`Global.asax.cs:68`, read with `bool.Parse`, which throws if the key is missing).
- Controllers take `ICatalogService` by constructor (`CatalogController.cs:16`, `PicController.cs:17`, `BrandsController.cs:16`, `FilesController.cs:15`).
- `CatalogController.Dispose` also disposes the injected service (`CatalogController.cs:142-150`). In DB mode this disposes the request-scoped `CatalogDBContext` that Autofac also disposes.

### 4.2 Data access

- **EF6 code-first** with fluent mapping in `Models/CatalogDBContext.cs:20-86`. Tables: `Catalog` (`:59`), `CatalogBrand` (`:45`), `CatalogType` (`:31`).
  Constraints: `Name` required, max 50 (`:67-69`); `Price` required (`:71-72`, no explicit precision, so the EF6 default is `decimal(18,2)`); `PictureFileName` required (`:74-75`);
  `PictureUri` ignored (`:77`); required FKs to brand and type (`:79-85`); `Brand`/`Type` required, max 100 (`:38-40`, `:52-54`).
- **Catalog item IDs are not identity columns.** `HasDatabaseGeneratedOption(None)` (`:63-65`). IDs come from:
  - **Runtime creates** – `CatalogItemHiLoGenerator` (`Models/CatalogItemHiLoGenerator.cs`). It's a singleton with a lock. It reserves a block by calling `SELECT NEXT VALUE FOR catalog_hilo` (`:22`) and hands out the next 9 values in memory (`:11,24,29-30`). The block size `HiLoIncrement = 10` (`:11`) **must match** the sequence's `INCREMENT BY 10` (`Models/Infrastructure/dbo.catalog_hilo.Sequence.sql:7`). Used by `CatalogService.CreateCatalogItem` (`Services/CatalogService.cs:53`).
  - **Seed brands/types** – one `NEXT VALUE FOR catalog_brand_hilo` / `catalog_type_hilo`, then +1 per row (`CatalogDBInitializer.cs:51-57,68-74,326-331`).
  - **Mock mode** – `max(Id) + 1` (`Services/CatalogServiceMock.cs:50-51`).
- **DB initialization** happens only when `UseMockData=false` (`Global.asax.cs:83-91`). `Database.SetInitializer(CatalogDBInitializer)`, which derives from `CreateDatabaseIfNotExists` (`CatalogDBInitializer.cs:15`). EF6 runs it lazily on the first context use, i.e. the first request. `Seed` (`:32-43`):
  1. Runs the three sequence scripts from `AppDomain.CurrentDomain.BaseDirectory` + `Models\Infrastructure\*.sql` (`:19-21,333-337`). The scripts are copied to output (`eShopLegacyMVC.csproj:304-312`). Each script starts with `USE [Microsoft.eShopOnContainers.Services.CatalogDb]` (e.g. `dbo.catalog_hilo.Sequence.sql:2`).
  2. Seeds types, brands and items from `PreconfiguredData` (`Models/Infrastructure/PreconfiguredData.cs`: 4 types, 5 brands, 12 items), **or** from `Setup/*.csv` when `UseCustomizationData=true` (`CatalogDBInitializer.cs:29,47-49,64-66,81-83`).
  3. When `UseCustomizationData=true`, deletes every file in `Pics/` and extracts `Setup/CatalogItems.zip` there (`:339-354`).
- **Connection string** `CatalogDBContext`: LocalDB, Integrated Security, MARS on, database `Microsoft.eShopOnContainers.Services.CatalogDb` (`Web.config:12`). The EF provider is `System.Data.SqlClient` (`Web.config:110`).
- **Service methods** (`Services/ICatalogService.cs:8-17`): paginated list (ordered by `Id`, includes brand and type; `CatalogService.cs:21-35`), find by id (`:37-40`), list brands/types (returns the `DbSet` directly, `:41-49`), create/update (attach + `Modified`)/remove (`:51-68`).
- **Mock service** keeps an in-memory `List<CatalogItem>` in a singleton (`CatalogServiceMock.cs:12-17`). Brand and type navigation properties are filled only inside `GetCatalogItemsPaginated` (`:21,73-82`), which mutates the shared items.

### 4.3 Logging

- log4net, configured by the assembly attribute `Properties/AssemblyInfo.cs:37` (`ConfigFile = "log4net.xml"`; the file on disk is `log4Net.xml`).
- `log4Net.xml`: root level `ALL` (`:3`), one `RollingFileAppender` to `logFiles\myapp.log` (`:6-7`), size-based, 10 MB × 5 backups (`:9-11`).
  Pattern: `%date [%thread] %property{activity} %level %logger - %property{requestinfo}%newline%message` (`:14`).
- Per-request context set in `Application_BeginRequest` (`Global.asax.cs:47-55`): `activityid` (`:50`) and `requestinfo` = `RawUrl, UserAgent` (`:52`, `:108-114`).
  **The pattern reads `activity` but the code sets `activityid`**, so the activity column is always `(null)`. You can see this in a runtime log on disk, `logFiles/myapp.log:1`.
- Loggers: `MvcApplication` (`Global.asax.cs:23`), `CatalogController` (`CatalogController.cs:12`, Info on every action, Debug on dispose), `PicController` (`PicController.cs:11`).
  Nothing logs exceptions explicitly.
- **Application Insights 2.9.1** is wired via HTTP modules (`Web.config:35-36,87-90`) and `ApplicationInsights.config`. **No instrumentation key appears anywhere in source** (`ApplicationInsights.config:138` is a comment). The source alone can't tell us whether telemetry was ever sent, for example via an environment variable.

### 4.4 Configuration

| Key | Where defined | Read at | Notes |
|---|---|---|---|
| `UseMockData` | `Web.config:19` (**`false`**) | `Global.asax.cs:68,85` | Picks mock vs EF service and whether DB initializer runs |
| `UseCustomizationData` | `Web.config:20` (`false`) | `CatalogDBInitializer.cs:29` | CSV/ZIP seeding |
| `ClientValidationEnabled`, `UnobtrusiveJavaScriptEnabled` | `Web.config:17-18` (`true`) | MVC runtime | Default behavior in ASP.NET Core |
| `webpages:Version`, `webpages:Enabled` | `Web.config:15-16`, `Views/Web.config:26` | Razor runtime | Not needed |
| Connection string `CatalogDBContext` | `Web.config:12` | `CatalogDBContext.cs:10` | |
| Culture `en-US` / UI culture `en-US` | `Web.config:38` | ASP.NET runtime | Affects `{0:C}` price display, date in footer, and decimal model binding (R-12) |
| Session `InProc` | `Web.config:33` | | |
| `compilation debug="true"` / `httpRuntime targetFramework="4.6.1"` | `Web.config:31-32` | | `debug` controls bundling/minification (4.6). `httpRuntime` 4.6.1 ≠ compiled 4.8 |

Transforms: `Web.Debug.config` only has comments. `Web.Release.config:18` only removes `debug` from `<compilation>`.
No environment-specific connection strings or settings exist.

### 4.5 Session

- InProc (`Web.config:33`), served by the async session module (`Web.config:91-92`).
- `Session_Start` stores `MachineName` and `SessionStartTime = DateTime.Now` (`Global.asax.cs:41-45`).
- The **only reader** is the footer, which prints `"{MachineName}, {SessionStartTime}"` on every page (`Views/Shared/_Layout.cshtml:35-36`). Nothing else uses session; there's no authentication, cart or TempData.

### 4.6 Bundling and static content

- Bundles (`App_Start/BundleConfig.cs:10-29`):
  `~/bundles/jquery` = `jquery-{version}.js`; `~/bundles/jqueryval` = `jquery.validate*`; `~/bundles/modernizr` = `modernizr-*`;
  `~/bundles/bootstrap` = `bootstrap.js` + `respond.js`; `~/Content/css` = `bootstrap.css`, `custom.css`, `base.css`, `site.css`.
- Rendered in `_Layout.cshtml:7-8` (css, modernizr in `<head>`), `:43-44` (jquery, bootstrap at end of body), and `Create.cshtml:103` / `Edit.cshtml:104` (jqueryval via `@section Scripts`).
- With `debug="true"` (`Web.config:31`) the files are emitted individually and unminified. Under Release they're bundled and minified.
- **Two Modernizr files match `modernizr-*`** (`Scripts/modernizr-2.6.2.js`, `Scripts/modernizr-2.8.3.js`). The exact tags emitted should be confirmed from page source of the running legacy app.
- Popper isn't in any bundle, although `bootstrap.js` 4.3.1 is the non-bundle build. No view uses dropdowns or tooltips, so nothing breaks today *(inspection)*.
- CSS references fonts via `../fonts/…` (`Content/base.css:4,10`) and the banner via `../images/main_banner.png` (`Content/Site.css:3`).
- Images: `Images/` (layout chrome), `Pics/` (product images, served **only** through `PicController`, not as static files), `favicon.ico`.

### 4.7 Error handling

- Global `HandleErrorAttribute` (`App_Start/FilterConfig.cs:10`). There's no `<customErrors>` in `Web.config`, so ASP.NET's default `RemoteOnly` applies: local requests see the YSOD and remote requests see `Views/Shared/Error.cshtml`.
- `Error.cshtml` is a complete HTML document, but `_ViewStart.cshtml:2` assigns the layout to every view, so it would render inside `_Layout` *(inspection)*.
- 400/404 come from `HttpStatusCodeResult`/`HttpNotFound` (`CatalogController.cs:36,41,82,87,119,124`; `PicController.cs:31,49`), with IIS default error pages (no `<httpErrors>` config).
- Web API errors use Web API's default error responses. There's no `Application_Error` and no exception logging. AI's `ExceptionTrackingTelemetryModule` is configured (`ApplicationInsights.config:113`) but has no key.

---

## 5. Controllers and routes (verified)

Routing setup: `Global.asax.cs:30` registers Web API **before** MVC (`:33`), so Web API routes win for `api/…`.
Web API: attribute routes + `api/{controller}/{id}` with optional id (`App_Start/WebApiConfig.cs:10-16`).
MVC: attribute routes + `{controller}/{action}/{id}` with defaults `Catalog/Index`, id optional (`App_Start/RouteConfig.cs:10-17`).
In MVC 5, attribute-routed actions are **not** reachable through conventional routes, so `/Pic/Index/…` doesn't exist.

### MVC controllers

| Controller | Action | HTTP verb | Route | View |
|---|---|---|---|---|
| CatalogController | Index(pageSize = 10, pageIndex = 0) (`CatalogController.cs:22`) | GET | `/`, `/Catalog`, `/Catalog/Index` (convention; paging via query string) | `Catalog/Index` (+ partial `Catalog/CatalogTable`) |
| CatalogController | Details(int? id) (`:31`) | GET | `/Catalog/Details/{id}` (convention; no id → 400, unknown → 404) | `Catalog/Details` |
| CatalogController | Create() (`:49`) | GET | `/Catalog/Create` (convention) | `Catalog/Create` |
| CatalogController | Create(CatalogItem) (`:62`) | POST | `/Catalog/Create` (convention; antiforgery `:61`) | Redirect to `Index`; `Catalog/Create` if invalid |
| CatalogController | Edit(int? id) (`:77`) | GET | `/Catalog/Edit/{id}` (convention; no id → 400, unknown → 404) | `Catalog/Edit` |
| CatalogController | Edit(CatalogItem) (`:100`) | POST | `/Catalog/Edit/{id}` (convention; antiforgery `:99`) | Redirect to `Index`; `Catalog/Edit` if invalid |
| CatalogController | Delete(int? id) (`:114`) | GET | `/Catalog/Delete/{id}` (convention; no id → 400, unknown → 404) | `Catalog/Delete` |
| CatalogController | DeleteConfirmed(int id), `[ActionName("Delete")]` (`:132-134`) | POST | `/Catalog/Delete/{id}` (convention; antiforgery `:133`) | Redirect to `Index` |
| PicController | Index(int catalogItemId) (`PicController.cs:25`) | GET | `/items/{catalogItemId:int}/pic` (attribute, route name `GetPicRouteTemplate`, `:13,24`) | None. Returns the image bytes from `~/Pics` with a MIME type from the extension (`:38-46,53-89`); ≤ 0 → 400, unknown item → 404 |

### API controllers

| Controller | Action | HTTP verb | Route | Returns |
|---|---|---|---|---|
| WebApi/BrandsController | Get() (`BrandsController.cs:22`) | GET | `/api/brands` (convention) | `IEnumerable<CatalogBrand>`, serialized by Web API's formatters (JSON via Newtonsoft with PascalCase names, or XML via content negotiation) |
| WebApi/BrandsController | Get(int id) (`:29`) | GET | `/api/brands/{id}` (convention) | 200 + `CatalogBrand`, or 404 |
| WebApi/BrandsController | Delete(int id) (`:38-40`) | DELETE | `/api/brands/{id}` (convention) | 200 (empty) or 404. **Demo only: nothing is deleted** (`:48`) |
| WebApi/FilesController | Get() (`FilesController.cs:21`) | GET | `/api/files` (convention) | 200, body = `List<BrandDTO>` serialized with `BinaryFormatter` (`:23-35`). No Content-Type is set |
| Api/CatalogController2 (MVC `Controller`) | Index() (`Api/CatalogController.cs:9`) | GET | `[Route("api")]` on the class (`:5`), i.e. `/api` | `Json(new { Message = "Hello World!" })` (`:11`), but see R-8: **most likely unreachable** |

---

## 6. Risks and open questions

Severity: **H** = blocks the port or changes behavior visibly; **M** = needs a decision or extra work; **L** = minor or cosmetic.

| # | Sev | Finding | Source |
|---|---|---|---|
| R-1 | H | **`/api/files` can't be ported as-is.** It returns a `BinaryFormatter` payload, and BinaryFormatter throws on .NET 9+. Decide: drop the endpoint, change the format (breaking for any client), or keep a .NET Framework sidecar. No consumer of this endpoint exists in the repo. | `Controllers/WebApi/FilesController.cs:29-32`; `eShopLegacy.Utilities/Serializing.cs:11-12,19-21` |
| R-2 | H | **Which data mode is the baseline?** The committed `Web.config` has `UseMockData=false`, so the app uses EF6 + LocalDB. AGENTS.md and the baseline commit message say it runs on mock data with `UseMockData=true`. Smoke tests and parity depend on this. | `Web.config:19`; `AGENTS.md` (Commands section); commit `5fb35c2` |
| R-3 | H | **LocalDB with Integrated Security** is Windows-only. The Linux container and Azure SQL need SQL auth and a different connection string. | `Web.config:12` |
| R-4 | H | **Seed SQL scripts hard-code `USE [Microsoft.eShopOnContainers.Services.CatalogDb]`.** They break if the database name changes, and `USE` isn't supported on Azure SQL Database. | `Models/Infrastructure/dbo.catalog_hilo.Sequence.sql:2`, `dbo.catalog_brand_hilo.Sequence.sql:1`, `dbo.catalog_type_hilo.Sequence.sql:1` |
| R-5 | M | Script paths use backslashes relative to `AppDomain.BaseDirectory`, so they fail on Linux. | `Models/Infrastructure/CatalogDBInitializer.cs:19-21,335` |
| R-6 | H | **Case-sensitive paths on Linux:** `log4net.xml` vs `log4Net.xml`; bundle `~/Content/site.css` vs file `Content/Site.css`; `~/images/…` and `../images/…` vs folder `Images/`. All work on Windows/IIS but 404 or fail on Linux. | `Properties/AssemblyInfo.cs:37`; `App_Start/BundleConfig.cs:29`; `Views/Shared/_Layout.cshtml:14,30,33`; `Content/Site.css:3` |
| R-7 | H | **API JSON contract changes by default.** Web API 2 serializes with Newtonsoft and keeps PascalCase (`Id`, `Brand`), while ASP.NET Core's System.Text.Json uses camelCase. Web API 2 also negotiates XML; ASP.NET Core doesn't unless configured. Decide whether to preserve PascalCase (`JsonSerializerOptions.PropertyNamingPolicy = null`) and/or XML. | `Controllers/WebApi/BrandsController.cs:22-35`; `App_Start/WebApiConfig.cs` (default formatters) |
| R-8 | M | **`CatalogController2` is almost certainly dead code** *(inspection)*. Its class name doesn't end in `Controller`, so neither MVC 5's controller discovery, attribute-route scanning nor Autofac's `RegisterControllers` picks it up. Even if reached, MVC 5 `Json()` on a GET without `JsonRequestBehavior.AllowGet` throws. ASP.NET Core *would* discover it by attribute and serve `/api`. Confirm by requesting `/api` on the running legacy app, then decide: drop it or port it. | `Controllers/Api/CatalogController.cs:5-11`; `Global.asax.cs:65` |
| R-9 | H | **Path traversal via `PictureFileName`.** Create and Edit bind `PictureFileName` from the POST body; the Edit field is only `readonly` in the browser. `PicController` then does `Path.Combine(MapPath("~/Pics"), PictureFileName)` and returns the bytes, so a crafted POST can make `/items/{id}/pic` serve any readable file (e.g. `..\Web.config`) *(inspection)*. Preserving behavior would preserve the hole. Decision needed. | `Controllers/CatalogController.cs:62,100`; `Views/Catalog/Edit.cshtml:63`; `Controllers/PicController.cs:38-44` |
| R-10 | M | Picture URLs are absolute and built from the request scheme and host. Behind a reverse proxy or Azure ingress they need forwarded-headers middleware, or they'll point at the internal host or scheme. | `Controllers/CatalogController.cs:162` |
| R-11 | M | **Footer depends on `Session_Start`,** which has no ASP.NET Core equivalent. You need session middleware plus "set on first access" logic. Session stores strings, so `SessionStartTime` must be formatted. In a scaled-out or container setup, the machine name becomes the container hostname. | `Global.asax.cs:41-45`; `Views/Shared/_Layout.cshtml:35-36`; `Web.config:33,91-92` |
| R-12 | M | **Culture-dependent output and parsing.** Culture is forced to `en-US`. Price uses `DataType.Currency` (renders `{0:C}`), the footer prints `DateTime.Now`, and decimal Price binding uses the culture. Linux containers may default to invariant culture or lack ICU. The port must set `en-US` explicitly. | `Web.config:38`; `Models/CatalogItem.cs:22-25`; `_Layout.cshtml:35` |
| R-13 | L | **log4net activity id is never logged:** the pattern uses `%property{activity}`, the code sets `activityid`. Decide whether to "fix" it in the port, since the target is `ILogger` anyway. | `log4Net.xml:14`; `Global.asax.cs:50`; `logFiles/myapp.log:1` |
| R-14 | M | **Mock-mode quirks that parity tests may hit** *(inspection)*: the mock is a non-thread-safe singleton `List` shared by all users. Brand and type are null on Details, Edit and Delete until the Index page has been rendered once (nav properties are filled only during pagination), and an edited item loses them again until the next Index. | `Modules/ApplicationModule.cs:20-22`; `Services/CatalogServiceMock.cs:12-17,33-36,55-61,73-82` |
| R-15 | M | **Delete of a missing id differs by mode** *(inspection)*: the DB path calls `Remove(null)`, which throws (500); the mock's `List.Remove(null)` is a no-op (302 to Index). Separately, the controller disposes the injected service, which shouldn't be carried over to ASP.NET Core. | `Controllers/CatalogController.cs:134-139,142-150`; `Services/CatalogService.cs:64-68`; `Services/CatalogServiceMock.cs:64-67` |
| R-16 | L | Paging edge cases *(inspection)*: `pageSize=0` throws `DivideByZeroException`. The hidden "Previous" link on page 0 points to `pageIndex=-1`; a negative `Skip` throws in SQL mode but is harmless in mock mode. | `ViewModel/PaginatedItemsViewModel.cs:23`; `Views/Catalog/Index.cshtml:25`; `Services/CatalogService.cs:29` |
| R-17 | M | **Static libraries on disk don't match package versions:** jQuery 3.3.1 on disk vs 3.5.0 package; jQuery Validation 1.17.0 vs 1.19.4. The app *serves* the on-disk files. jQuery 3.3.1 has known XSS advisories that were fixed in 3.5.0. Decide: copy the served versions for parity, or the package versions. | `Scripts/jquery-3.3.1.js`, `Scripts/jquery.validate.js` headers; `eShopLegacyMVC.csproj:480-487` |
| R-18 | L | `modernizr-*` wildcard matches two versions. Respond and Modernizr are legacy-IE polyfills with no dependents in the app. | `App_Start/BundleConfig.cs:18-19,21-23`; `Scripts/modernizr-2.6.2.js`, `Scripts/modernizr-2.8.3.js` |
| R-19 | M | Seed data couples hard-coded FK ids (1–5) to sequences starting at 1. The seed works only on a fresh database whose sequences start at 1. | `Models/Infrastructure/PreconfiguredData.cs:14-25`; `CatalogDBInitializer.cs:51-57,68-74`; `dbo.catalog_*_hilo.Sequence.sql` (`START WITH 1`) |
| R-20 | M | With `UseCustomizationData=true`, seeding **deletes and rewrites `Pics/`**. That needs a writable content directory, which a read-only container filesystem doesn't have. | `Models/Infrastructure/CatalogDBInitializer.cs:339-354` |
| R-21 | M | HiLo block size is duplicated in code (10) and SQL (`INCREMENT BY 10`). EF Core's `UseHiLo("catalog_hilo")` reads the increment from the sequence and would replace `CatalogItemHiLoGenerator`. Decide: keep the custom generator (closest to legacy) or use the built-in one. | `Models/CatalogItemHiLoGenerator.cs:11,22`; `dbo.catalog_hilo.Sequence.sql:7` |
| R-22 | M | Can the new app point at a database **created by the legacy app**? EF6 creates `__MigrationHistory` and its own schema conventions. EF Core's `EnsureCreated` won't touch an existing DB, but schema compatibility (decimal precision, FK names) can't be confirmed without the database. | `Models/CatalogDBContext.cs:20-86`; `CatalogDBInitializer.cs:15` |
| R-23 | L | Error page parity: `HandleErrorAttribute` with default `RemoteOnly` means local users see the YSOD. `Error.cshtml` is a full HTML page that would be wrapped by the layout. What should the port show? | `App_Start/FilterConfig.cs:10`; `Views/Shared/Error.cshtml`; `Views/_ViewStart.cshtml:2` |
| R-24 | L | Small legacy quirks a pixel-level parity check would expose: the Edit form labels show raw `CatalogBrandId`/`CatalogTypeId` (Create shows `Brand`/`Type`); an invalid Edit POST re-renders without `PictureUri` (broken image); the pager text shows the page size, not the item count. | `Views/Catalog/Edit.cshtml:37,45`; `Controllers/CatalogController.cs:104-111`; `Views/Catalog/Index.cshtml:29` |
| R-25 | L | Copy-paste bugs in seeding error messages: the brand check reports "type=…", and the restock/max-stock messages print the parsed int instead of the input. | `Models/Infrastructure/CatalogDBInitializer.cs:208,256,273` |
| R-26 | L | Application Insights: no instrumentation key in source, so the source can't show whether telemetry was ever collected. Treat it as dropped until the Azure step. | `ApplicationInsights.config:138`; `Web.config:35-36,87-90` |
| R-27 | L | `eShopPorted` is in the solution and is built by `run-legacy.ps1`. It isn't part of the legacy app at runtime. Keep it in the legacy build? | `eShopLegacyMVC.sln:8`; `run-legacy.ps1:9` |
| R-28 | L | Views aren't compiled at build (`MvcBuildViews=false`), so latent view errors in legacy would only show at runtime. ASP.NET Core compiles views at build and will surface them. | `eShopLegacyMVC.csproj:20` |
