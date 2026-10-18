using System.Globalization;
using eShopMvc.Web.Infrastructure;
using eShopMvc.Web.Services;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options => options.Filters.Add<SessionStartFilter>())
    // Decision R-7: PascalCase JSON, as Web API 2 (Newtonsoft) produced. No XML formatter.
    .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);
builder.Services.AddCatalogServices(builder.Configuration);

// Decision R-11: session for the footer (legacy InProc session + Session_Start, 20-minute idle timeout).
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

await app.InitializeCatalogDatabaseAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // Decision R-23: generic error page; the middleware logs the exception.
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

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

app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Catalog}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

// Lets WebApplicationFactory<Program> in the integration tests reach the top-level program (Decision D14).
public partial class Program;
