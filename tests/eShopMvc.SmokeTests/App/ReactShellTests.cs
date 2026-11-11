using System.Net;
using eShopMvc.SmokeTests.Infrastructure;

namespace eShopMvc.SmokeTests.App;

/// <summary>
/// The React UI's page shell at the page URLs of the Razor UI (Decision P2-9). New app only; needs the
/// client built (<c>npm run build</c> in src/eShopMvc.Client) before the app starts. What React renders
/// into the shell is covered by the Vitest and browser tests.
/// </summary>
[Trait("Category", "App")]
public sealed class ReactShellTests
{
    [Theory(Skip = "React UI (P2-9)", SkipUnless = nameof(SmokeSettings.IsNew), SkipType = typeof(SmokeSettings))]
    [InlineData("/")]
    [InlineData("/Catalog")]
    [InlineData("/Catalog/Index?pageSize=3&pageIndex=1")]
    [InlineData("/Catalog/Create")]
    [InlineData("/Catalog/Details/1")]
    [InlineData("/Catalog/Edit/1")]
    [InlineData("/Catalog/Delete/1")]
    [InlineData("/catalog/details/2147483647")] // any id: React asks the API and says "not found" (P2-15)
    public async Task PageRoutes_ReturnTheShellWithTheRazorStyleSheets(string url)
    {
        using var client = new AppClient();

        var page = await client.GetPageAsync(url);

        Assert.NotNull(page.Find("div#root"));
        Assert.Equal(
            ["/lib/bootstrap/dist/css/bootstrap.min.css", "/css/custom.css", "/css/base.css", "/css/site.css"],
            page.All("link[rel=stylesheet]").Select(l => l.GetAttribute("href"))); // P2-11
        Assert.StartsWith("/app/assets/", page.Single("script[type=module]").GetAttribute("src"));
    }

    [Fact(Skip = "React UI (P2-9)", SkipUnless = nameof(SmokeSettings.IsNew), SkipType = typeof(SmokeSettings))]
    public async Task Shell_ScriptAndStyleSheetsAreServed_AndAMissingAssetIsNotTheShell()
    {
        using var client = new AppClient();
        var page = await client.GetPageAsync("/");

        using var script = await client.GetAsync(page.Single("script[type=module]").GetAttribute("src")!);
        using var missing = await client.GetAsync("/app/assets/missing.js");

        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Equal("text/javascript", script.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        foreach (var href in page.All("link[rel=stylesheet]").Select(l => l.GetAttribute("href")!))
        {
            using var styleSheet = await client.GetAsync(href);
            Assert.True(styleSheet.StatusCode == HttpStatusCode.OK, $"{(int)styleSheet.StatusCode} {href}");
        }
    }

    [Theory(Skip = "React UI (P2-9)", SkipUnless = nameof(SmokeSettings.IsNew), SkipType = typeof(SmokeSettings))]
    [InlineData("/no/such/page")]
    [InlineData("/Catalog/Nope")]
    [InlineData("/Catalog/Details")] // Razor UI: 400
    [InlineData("/Catalog/Details?id=1")] // Razor UI: the item
    [InlineData("/Catalog/Create/5")] // Razor UI: the form
    [InlineData("/app/")] // the side-by-side period (P2-4)
    [InlineData("/app/Catalog/Details/1")]
    public async Task OtherUrls_AreNotFound(string url)
    {
        using var client = new AppClient();

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(Skip = "React UI (P2-9)", SkipUnless = nameof(SmokeSettings.IsNew), SkipType = typeof(SmokeSettings))]
    public async Task RazorFormPost_IsNoLongerAccepted()
    {
        using var client = new AppClient();
        var name = CatalogUi.NewItemName();
        var fields = new Dictionary<string, string>();
        CatalogUi.FillValidItem(fields, name, brandId: "1", typeId: "1");

        using var response = await client.PostFormAsync("/Catalog/Create", fields);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Null(await CatalogApi.FindByNameAsync(client, name));
    }
}
