using System.Net;

namespace eShopMvc.Web.IntegrationTests.Web;

/// <summary>
/// Decision P2-4: the fallback from <c>/app/{**path}</c> to the React shell must not catch anything else.
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
    [InlineData("/api/v1/catalog/items/1/nope")]
    [InlineData("/application")]
    public async Task MissingFilesAndOtherPaths_StayNotFound(string path)
    {
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync(path, Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Catalog/Details/1")]
    public async Task RazorPages_AreStillServedByMvc(string path)
    {
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync(path, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("<div id=\"root\">", await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task ClientRoute_DoesNotStartASession()
    {
        // The shell is a static file (P2-8): only /api/v1/session starts the footer's session.
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync("/app/Catalog/Details/1", Token);

        Assert.False(response.Headers.Contains("Set-Cookie"));
    }
}
