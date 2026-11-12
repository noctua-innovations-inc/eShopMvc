using System.Globalization;
using eShopMvc.Web.Infrastructure;
using eShopMvc.Web.Services;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    // Decision R-7: PascalCase JSON, as Web API 2 (Newtonsoft) produced. No XML formatter.
    .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);
builder.Services.AddCatalogServices(builder.Configuration);

// Decision P2-7: the React client sends the antiforgery token in a header.
builder.Services.AddAntiforgery(options => options.HeaderName = AntiforgeryCookieFilter.HeaderName);

// No session: it only held the footer's values (R-11, P2-8), and the footer is gone (Decision P3-7).

// Decision L3: /health/live and /health/ready (AddCatalogServices adds the database check in database mode).
builder.Services.AddHealthChecks();

var app = builder.Build();

await app.InitializeCatalogDatabaseAsync();

// Configure the HTTP request pipeline.

// Decision L2: User-Agent in the logging scope of everything logged for the request (first, so it covers all).
app.UseMiddleware<UserAgentLoggingScopeMiddleware>();
// Decision P2-19: likewise the React page an API call was made for.
app.UseMiddleware<ClientPageLoggingScopeMiddleware>();

if (!app.Environment.IsDevelopment())
{
    // Decision R-23: generic error page; the middleware logs the exception.
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Decision D-D: no UseHttpsRedirection. TLS ends at the proxy / Azure front end, which also redirects;
// behind one, ASPNETCORE_FORWARDEDHEADERS_ENABLED=true lets the app see the original scheme (R-10).

// Decision R-12: always en-US, as legacy's <globalization culture="en-US" uiCulture="en-US" />.
// No culture providers, so Accept-Language can't change price binding or formatting;
// no user overrides, so the host's regional settings can't either.
var enUS = new CultureInfo("en-US", useUserOverride: false);
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(enUS),
    SupportedCultures = [enUS],
    SupportedUICultures = [enUS],
    RequestCultureProviders = [],
});

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

// The API, the pictures and the error page: all attribute-routed. The legacy default route
// ({controller=Catalog}/{action=Index}/{id?}) went with the Razor catalog pages (Decision P2-9).
app.MapControllers()
    .WithStaticAssets();

// Decision P2-9: the page URLs of the Razor UI get the React shell (built into wwwroot/app, served under
// /app/); React Router shows the page. Only these: any other URL stays a 404.
string[] pageRoutes =
[
    "/", "/Catalog", "/Catalog/Index", "/Catalog/Create",
    "/Catalog/Details/{id}", "/Catalog/Edit/{id}", "/Catalog/Delete/{id}",
];
foreach (var pageRoute in pageRoutes)
{
    app.MapFallbackToFile(pageRoute, "app/index.html");
}

app.MapCatalogHealthChecks();

app.Run();

// Lets WebApplicationFactory<Program> in the integration tests reach the top-level program (Decision D14).
public partial class Program;
