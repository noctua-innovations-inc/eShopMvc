using System.Net;
using eShopMvc.SmokeTests.Infrastructure;

namespace eShopMvc.SmokeTests.Catalog;

[Trait("Category", "Catalog")]
public sealed class DeleteTests
{
    [Fact(Skip = "Razor UI: legacy only since the cutover (P2-9)", SkipUnless = nameof(SmokeSettings.IsLegacy), SkipType = typeof(SmokeSettings))]
    public async Task DeletePage_AsksForConfirmationWithTokenForm()
    {
        using var client = new AppClient();
        await using var item = await CatalogItemScope.CreateAsync(client);

        var page = await client.GetPageAsync($"/Catalog/Delete/{item.Id}");

        Assert.Contains("Are you sure you want to delete this?", page.BodyText);
        Assert.Contains(item.Name, page.All("dd").Select(dd => HtmlPage.Normalize(dd.TextContent)));
        Assert.False(string.IsNullOrEmpty(page.AntiforgeryToken));
    }

    [Fact(Skip = "Razor UI: legacy only since the cutover (P2-9)", SkipUnless = nameof(SmokeSettings.IsLegacy), SkipType = typeof(SmokeSettings))]
    public async Task Delete_Confirmed_RedirectsToIndexAndItemIsGone()
    {
        using var client = new AppClient();
        await using var item = await CatalogItemScope.CreateAsync(client);
        var form = await client.GetPageAsync($"/Catalog/Delete/{item.Id}");

        using (var response = await client.PostFormAsync($"/Catalog/Delete/{item.Id}", form.FormFields()))
        {
            CatalogUi.AssertRedirectToIndex(response);
        }

        using var details = await client.GetAsync($"/Catalog/Details/{item.Id}");
        Assert.Equal(HttpStatusCode.NotFound, details.StatusCode);
    }
}
