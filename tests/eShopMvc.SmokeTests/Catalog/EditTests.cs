using System.Net;
using eShopMvc.SmokeTests.Infrastructure;

namespace eShopMvc.SmokeTests.Catalog;

[Trait("Category", "Catalog")]
public sealed class EditTests
{
    [Fact]
    public async Task EditForm_IsPrefilledWithItem()
    {
        using var client = new AppClient();
        await using var item = await CatalogItemScope.CreateAsync(client);

        var page = await client.GetPageAsync($"/Catalog/Edit/{item.Id}");

        Assert.False(string.IsNullOrEmpty(page.AntiforgeryToken));
        Assert.Equal(item.Name, page.Single("input[name='Name']").GetAttribute("value"));
        var id = page.Single("input[name='Id']");
        Assert.Equal("hidden", id.GetAttribute("type"));
        Assert.Equal(item.Id.ToString(), id.GetAttribute("value"));
        Assert.True(page.Single("input[name='PictureFileName']").HasAttribute("readonly"), "PictureFileName should be read-only.");
    }

    [Fact]
    public async Task Edit_ValidChanges_RedirectsToIndexAndDetailsShowNewValues()
    {
        using var client = new AppClient();
        await using var item = await CatalogItemScope.CreateAsync(client);
        var newName = CatalogUi.NewItemName();
        (string Value, string Text) brand = (string.Empty, string.Empty), type = (string.Empty, string.Empty);

        using (var response = await CatalogUi.PostEditAsync(client, item.Id, (form, fields) =>
               {
                   brand = form.SelectOptions("CatalogBrandId")[^1];
                   type = form.SelectOptions("CatalogTypeId")[^1];
                   CatalogUi.FillValidItem(fields, newName, brand.Value, type.Value, price: "23.45");
               }))
        {
            CatalogUi.AssertRedirectToIndex(response);
        }

        var details = await client.GetPageAsync($"/Catalog/Details/{item.Id}");
        var values = details.All("dd").Select(dd => HtmlPage.Normalize(dd.TextContent)).ToList();
        Assert.Contains(newName, values);
        Assert.Contains(brand.Text, values);
        Assert.Contains(type.Text, values);
        Assert.Contains(values, v => v.Contains("23.45", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Edit_InvalidPrice_ReturnsFormWithValidationErrorAndKeepsItem()
    {
        using var client = new AppClient();
        await using var item = await CatalogItemScope.CreateAsync(client);

        using var response = await CatalogUi.PostEditAsync(client, item.Id, (_, fields) =>
        {
            fields["Name"] = CatalogUi.NewItemName();
            fields["Price"] = "abc";
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = HtmlPage.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), $"/Catalog/Edit/{item.Id}");
        Assert.True(page.HasValidationError, "Expected a field validation error on the re-rendered form.");

        var details = await client.GetPageAsync($"/Catalog/Details/{item.Id}");
        var values = details.All("dd").Select(dd => HtmlPage.Normalize(dd.TextContent)).ToList();
        Assert.Contains(item.Name, values);
        Assert.Contains(values, v => v.Contains(item.Price, StringComparison.Ordinal));
    }
}
