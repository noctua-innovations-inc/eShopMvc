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
        var (brand, type) = await CatalogApi.FirstBrandAndTypeAsync(client);
        var body = CatalogApi.ValidItem(name, brand.Id, type.Id);
        body["PictureFileName"] = TraversalFileName; // not on the React Create form, but accepted by the API

        using var response = await client.SendJsonAsync(HttpMethod.Post, CatalogApi.Items, body);

        var created = await CatalogApi.FindByNameAsync(client, name);
        if (created is not null)
        {
            await CatalogItemScope.Adopt(created.Id, name).DisposeAsync();
        }

        await AssertPictureFileNameErrorAsync(response);
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

        var (brand, type) = await CatalogApi.FirstBrandAndTypeAsync(client);
        var body = CatalogApi.ValidItem(item.Name, brand.Id, type.Id);
        body["Id"] = item.Id;
        body["PictureFileName"] = TraversalFileName;

        using (var response = await client.SendJsonAsync(HttpMethod.Put, $"{CatalogApi.Items}/{item.Id}", body))
        {
            await AssertPictureFileNameErrorAsync(response);
        }

        using var picture = await client.GetAsync($"/items/{item.Id}/pic");
        Assert.Equal(HttpStatusCode.OK, picture.StatusCode);
        Assert.Equal("image/png", picture.Content.Headers.ContentType?.MediaType); // still dummy.png
    }

    /// <summary>The API's 400 with a message for PictureFileName only, which the React forms show (P2-16).</summary>
    private static async Task AssertPictureFileNameErrorAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["PictureFileName"], json.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name));
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
