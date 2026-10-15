using System.Net;
using eShopMvc.SmokeTests.Infrastructure;

namespace eShopMvc.SmokeTests.Security;

/// <summary>
/// Decision R-9: legacy binds <c>PictureFileName</c> from the POST body and serves
/// <c>Path.Combine(~/Pics, PictureFileName)</c>, so a crafted value reads files outside <c>Pics</c>.
/// The new app rejects such values with a form validation error (and its picture endpoint also
/// refuses paths outside <c>Pics</c>). The legacy cases document the hole; the new cases guard the fix.
/// </summary>
[Trait("Category", "Security")]
[Trait("Category", "Target")]
public sealed class PictureFileNameTests
{
    private const string TraversalFileName = @"..\Web.config";

    [Fact(Skip = "Legacy-only behavior (R-9)", SkipUnless = nameof(SmokeSettings.IsLegacy), SkipType = typeof(SmokeSettings))]
    public async Task Create_Legacy_AcceptsTraversalAndPictureEndpointServesWebConfig()
    {
        using var client = new AppClient();
        var name = CatalogUi.NewItemName();

        using (var response = await PostCreateWithTraversalAsync(client, name))
        {
            CatalogUi.AssertRedirectToIndex(response);
        }

        var row = await CatalogUi.FindByNameAsync(client, name);
        Assert.NotNull(row);
        await using var item = CatalogItemScope.Adopt(row.Id, name);

        await AssertPictureServesWebConfigAsync(client, item.Id);
    }

    [Fact(Skip = "New-app behavior (R-9)", SkipUnless = nameof(SmokeSettings.IsNew), SkipType = typeof(SmokeSettings))]
    public async Task Create_New_RejectsTraversalWithValidationError()
    {
        using var client = new AppClient();
        var name = CatalogUi.NewItemName();

        using var response = await PostCreateWithTraversalAsync(client, name);

        var created = await CatalogUi.FindByNameAsync(client, name);
        if (created is not null)
        {
            await CatalogItemScope.Adopt(created.Id, name).DisposeAsync();
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = HtmlPage.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), "/Catalog/Create");
        Assert.True(page.HasValidationError, "Expected a field validation error for PictureFileName.");
        Assert.Null(created);
    }

    [Fact(Skip = "Legacy-only behavior (R-9)", SkipUnless = nameof(SmokeSettings.IsLegacy), SkipType = typeof(SmokeSettings))]
    public async Task Edit_Legacy_AcceptsTraversalAndPictureEndpointServesWebConfig()
    {
        using var client = new AppClient();
        await using var item = await CatalogItemScope.CreateAsync(client);

        using (var response = await CatalogUi.PostEditAsync(client, item.Id, (_, fields) => fields["PictureFileName"] = TraversalFileName))
        {
            CatalogUi.AssertRedirectToIndex(response);
        }

        await AssertPictureServesWebConfigAsync(client, item.Id);
    }

    [Fact(Skip = "New-app behavior (R-9)", SkipUnless = nameof(SmokeSettings.IsNew), SkipType = typeof(SmokeSettings))]
    public async Task Edit_New_RejectsTraversalAndPictureIsUnchanged()
    {
        using var client = new AppClient();
        await using var item = await CatalogItemScope.CreateAsync(client);

        using (var response = await CatalogUi.PostEditAsync(client, item.Id, (_, fields) => fields["PictureFileName"] = TraversalFileName))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = HtmlPage.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), $"/Catalog/Edit/{item.Id}");
            Assert.True(page.HasValidationError, "Expected a field validation error for PictureFileName.");
        }

        using var picture = await client.GetAsync($"/items/{item.Id}/pic");
        Assert.Equal(HttpStatusCode.OK, picture.StatusCode);
        Assert.Equal("image/png", picture.Content.Headers.ContentType?.MediaType); // still dummy.png
    }

    private static Task<HttpResponseMessage> PostCreateWithTraversalAsync(AppClient client, string name) =>
        CatalogUi.PostCreateAsync(client, (form, fields) =>
        {
            CatalogUi.FillValidItem(fields, name, form.SelectOptions("CatalogBrandId")[0].Value, form.SelectOptions("CatalogTypeId")[0].Value);
            fields["PictureFileName"] = TraversalFileName; // not rendered by the Create form, but bound by the action
        });

    private static async Task AssertPictureServesWebConfigAsync(AppClient client, int id)
    {
        using var picture = await client.GetAsync($"/items/{id}/pic");
        Assert.Equal(HttpStatusCode.OK, picture.StatusCode);
        var body = await picture.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("<configuration>", body);
    }
}
