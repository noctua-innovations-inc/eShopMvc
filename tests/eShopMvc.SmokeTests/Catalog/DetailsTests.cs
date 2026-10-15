using System.Net;
using eShopMvc.SmokeTests.Infrastructure;

namespace eShopMvc.SmokeTests.Catalog;

/// <summary>Details page, plus the 400/404 cases shared by Details, Edit and Delete.</summary>
[Trait("Category", "Catalog")]
public sealed class DetailsTests
{
    private const int MissingId = int.MaxValue;

    [Theory]
    [InlineData("Details")]
    [InlineData("Edit")]
    [InlineData("Delete")]
    public async Task ItemPage_WithoutId_Returns400(string action)
    {
        using var client = new AppClient();

        using var response = await client.GetAsync($"/Catalog/{action}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Details")]
    [InlineData("Edit")]
    [InlineData("Delete")]
    public async Task ItemPage_UnknownId_Returns404(string action)
    {
        using var client = new AppClient();

        using var response = await client.GetAsync($"/Catalog/{action}/{MissingId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Details_ShowsItemValuesPictureAndEditLink()
    {
        using var client = new AppClient();
        await using var item = await CatalogItemScope.CreateAsync(client);

        var page = await client.GetPageAsync($"/Catalog/Details/{item.Id}");

        var values = page.All("dd").Select(dd => HtmlPage.Normalize(dd.TextContent)).ToList();
        Assert.Contains(item.Name, values);
        Assert.Contains(item.Description, values);
        Assert.Contains(item.BrandText, values);
        Assert.Contains(item.TypeText, values);
        Assert.Contains(values, v => v.Contains(item.Price, StringComparison.Ordinal));

        var picture = page.Single("img.esh-picture").GetAttribute("src")!;
        Assert.Equal($"/items/{item.Id}/pic", new Uri(SmokeSettings.BaseUrl, picture).AbsolutePath);
        page.Single($"a[href$='/Catalog/Edit/{item.Id}']");
    }
}
