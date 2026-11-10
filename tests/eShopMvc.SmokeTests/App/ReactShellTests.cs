using System.Net;
using eShopMvc.SmokeTests.Infrastructure;

namespace eShopMvc.SmokeTests.App;

/// <summary>
/// The React UI's page shell under <c>/app/</c> (Decision P2-4). New app only; needs the client built
/// (<c>npm run build</c> in src/eShopMvc.Client) before the app starts. What React renders into the
/// shell is covered by the Vitest and browser tests.
/// </summary>
[Trait("Category", "App")]
public sealed class ReactShellTests
{
    [Theory(Skip = "React UI (P2-4)", SkipUnless = nameof(SmokeSettings.IsNew), SkipType = typeof(SmokeSettings))]
    [InlineData("/app/")]
    [InlineData("/app/index.html")]
    [InlineData("/app/Catalog")]
    [InlineData("/app/Catalog/Index?pageSize=3&pageIndex=1")]
    [InlineData("/app/Catalog/Details/1")]
    public async Task ClientRoutes_ReturnTheShellWithTheRazorStyleSheets(string url)
    {
        using var client = new AppClient();

        var page = await client.GetPageAsync(url);

        Assert.NotNull(page.Find("div#root"));
        Assert.Equal(
            ["/lib/bootstrap/dist/css/bootstrap.min.css", "/css/custom.css", "/css/base.css", "/css/site.css"],
            page.All("link[rel=stylesheet]").Select(l => l.GetAttribute("href"))); // P2-11
        Assert.StartsWith("/app/assets/", page.Single("script[type=module]").GetAttribute("src"));
    }

    [Fact(Skip = "React UI (P2-4)", SkipUnless = nameof(SmokeSettings.IsNew), SkipType = typeof(SmokeSettings))]
    public async Task Shell_ScriptIsServed_AndAMissingAssetIsNotTheShell()
    {
        using var client = new AppClient();
        var page = await client.GetPageAsync("/app/");

        using var script = await client.GetAsync(page.Single("script[type=module]").GetAttribute("src")!);
        using var missing = await client.GetAsync("/app/assets/missing.js");

        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Equal("text/javascript", script.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
