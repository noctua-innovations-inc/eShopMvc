using System.Net;
using System.Net.Http.Headers;

namespace eShopMvc.Web.IntegrationTests.Web;

// The views beyond Index arrive in step 7; these cover routing and the step-6 decisions.
public sealed class CatalogPageTests(MockModeAppFactory factory) : IClassFixture<MockModeAppFactory>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/")]
    [InlineData("/Catalog")]
    [InlineData("/Catalog/Index")]
    public async Task Index_DefaultRoute_RendersCatalogTableWithPictureUrls(string path)
    {
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync(path, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(Token);
        Assert.Contains("<table class=\"table\">", html);
        Assert.Contains("src=\"http://localhost/items/1/pic\"", html); // absolute, as legacy
        Assert.Contains("href=\"/Catalog/Edit/1\"", html);
        // ActionLink(text, action, null, htmlAttributes) binds to a controllerName overload in ASP.NET Core.
        Assert.Contains("class=\"btn esh-button esh-button-primary\" href=\"/Catalog/Create\"", html);
    }

    [Fact]
    public async Task Index_PriceUsesEnUsRegardlessOfAcceptLanguage()
    {
        // Decision R-12.
        using var client = factory.CreateRawClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Catalog/Index?pageSize=1");
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("de-DE"));

        using var response = await client.SendAsync(request, Token);

        var html = await response.Content.ReadAsStringAsync(Token);
        Assert.Contains("$19.50", html);
        Assert.DoesNotContain("19,50", html);
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageIndex=-1")]
    [InlineData("pageSize=2147483647&pageIndex=2")]
    public async Task Index_InvalidPaging_Returns400(string query)
    {
        // Decision R-16.
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync($"/Catalog/Index?{query}", Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Details")]
    [InlineData("Edit")]
    [InlineData("Delete")]
    public async Task ItemPages_WithoutIdOrUnknownId_Return400Or404(string action)
    {
        using var client = factory.CreateRawClient();

        using var withoutId = await client.GetAsync($"/Catalog/{action}", Token);
        using var unknown = await client.GetAsync($"/Catalog/{action}/999", Token);

        Assert.Equal(HttpStatusCode.BadRequest, withoutId.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task CreatePost_WithoutAntiforgeryToken_Returns400()
    {
        using var client = factory.CreateRawClient();

        using var response = await client.PostAsync("/Catalog/Create",
            new FormUrlEncodedContent([new("Name", "x"), new("Price", "1")]), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
