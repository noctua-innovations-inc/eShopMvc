using System.Net;

namespace eShopMvc.Web.IntegrationTests.Web;

/// <summary>
/// Decision P2-9: only the page URLs of the Razor UI fall back to the React shell; nothing else does.
/// The shell itself (wwwroot/app, built by npm and git-ignored) is covered by the smoke tests; these
/// pass with or without it.
/// </summary>
public sealed class SpaFallbackTests(MockModeAppFactory factory) : IClassFixture<MockModeAppFactory>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/app/assets/missing.js")]
    [InlineData("/app/missing.css")]
    [InlineData("/api/v1/nope")]
    [InlineData("/api/v1/session")] // removed with the footer (P3-7)
    [InlineData("/api/v1/catalog/items/1/nope")]
    [InlineData("/no/such/page")]
    [InlineData("/Catalog/Nope")]
    // The side-by-side period's URLs (P2-4).
    [InlineData("/app/")]
    [InlineData("/app/Catalog/Details/1")]
    // Forms of the legacy default route that no link produces (P2-9).
    [InlineData("/Catalog/Details")]
    [InlineData("/Catalog/Details?id=1")]
    [InlineData("/Catalog/Edit")]
    [InlineData("/Catalog/Delete")]
    [InlineData("/Catalog/Create/5")]
    [InlineData("/Catalog/Index/5")]
    [InlineData("/Catalog/Details/1/extra")]
    public async Task EverythingButThePageRoutes_IsNotFound(string path)
    {
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync(path, Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Catalog")]
    [InlineData("/catalog/index?pageSize=5&pageIndex=1")]
    [InlineData("/Catalog/Create")]
    [InlineData("/Catalog/Details/1")]
    [InlineData("/Catalog/Edit/1")]
    [InlineData("/Catalog/Delete/abc")]
    public async Task PageRoutes_ReturnTheShellIfBuilt_AndSetNoCookie(string path)
    {
        // The shell is a static file; the app has no session since the footer went (P3-7).
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync(path, Token);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            Assert.Contains("<div id=\"root\">", await response.Content.ReadAsStringAsync(Token));
        }
        else
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // client not built
        }
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData("/Catalog/Create")]
    [InlineData("/Catalog/Edit/1")]
    [InlineData("/Catalog/Delete/1")]
    public async Task FormPostsOfTheRazorUi_AreNoLongerAccepted(string path)
    {
        // Only the JSON API writes data (P2-6).
        using var client = factory.CreateRawClient();

        using var response = await client.PostAsync(path, new FormUrlEncodedContent([new("Name", "x")]), Token);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        using var item = await client.GetAsync("/api/v1/catalog/items/1", Token);
        Assert.Equal(HttpStatusCode.OK, item.StatusCode);
    }
}
