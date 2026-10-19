using System.Net;

namespace eShopMvc.Web.IntegrationTests.Web;

// Decision D-D / R-10: picture URLs are absolute and built from the request scheme and host.
public sealed class ForwardedHeadersTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ForwardedHeadersEnabled_ForwardedProtoHttps_PictureUrlsUseHttps()
    {
        // ASPNETCORE_FORWARDEDHEADERS_ENABLED=true in a container = host setting FORWARDEDHEADERS_ENABLED.
        await using var factory = new SettingsAppFactory(("FORWARDEDHEADERS_ENABLED", "true"));
        using var client = factory.CreateRawClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Catalog/Index?pageSize=1");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("src=\"https://localhost/items/1/pic\"", await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task ForwardedHeadersOff_ForwardedProtoIsIgnored()
    {
        // The default (and local compose): a client can't change the scheme the app sees.
        await using var factory = new SettingsAppFactory();
        using var client = factory.CreateRawClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Catalog/Index?pageSize=1");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request, Token);

        Assert.Contains("src=\"http://localhost/items/1/pic\"", await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task PlainHttp_IsServedWithoutRedirect()
    {
        // Decision D-D: no UseHttpsRedirection; the proxy / Azure front end redirects.
        await using var factory = new SettingsAppFactory();
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync("/", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class SettingsAppFactory(params (string Key, string Value)[] settings) : MockModeAppFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        }
    }
}
