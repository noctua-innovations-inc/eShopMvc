using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace eShopMvc.Web.IntegrationTests.Web;

/// <summary>
/// The real app in-process (Decision D14), in mock mode so no database is involved.
/// Content root is src/eShopMvc.Web, so Pics and the views are the real ones.
/// </summary>
public class MockModeAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Catalog:UseMockData", "true");
    }

    /// <summary>A client that doesn't follow redirects, so status codes are the app's own.</summary>
    public HttpClient CreateRawClient() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
}
