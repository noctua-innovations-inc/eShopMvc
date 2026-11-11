using System.Net;
using eShopMvc.SmokeTests.Infrastructure;

namespace eShopMvc.SmokeTests.Catalog;

[Trait("Category", "Catalog")]
public sealed class CreateTests
{
    [Fact(Skip = "Razor UI: legacy only since the cutover (P2-9)", SkipUnless = nameof(SmokeSettings.IsLegacy), SkipType = typeof(SmokeSettings))]
    public async Task CreateForm_HasTokenInputsAndPopulatedSelects()
    {
        using var client = new AppClient();

        var page = await client.GetPageAsync("/Catalog/Create");

        Assert.EndsWith("/Catalog/Create", page.PostForm.GetAttribute("action") ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrEmpty(page.AntiforgeryToken));
        foreach (var name in new[] { "Name", "Description", "Price", "AvailableStock", "RestockThreshold", "MaxStockThreshold" })
        {
            Assert.True(page.PostForm.QuerySelector($"input[name='{name}']") is not null, $"Create form has no input '{name}'.");
        }

        Assert.NotEmpty(page.SelectOptions("CatalogBrandId"));
        Assert.NotEmpty(page.SelectOptions("CatalogTypeId"));
    }

    [Fact(Skip = "Razor UI: legacy only since the cutover (P2-9)", SkipUnless = nameof(SmokeSettings.IsLegacy), SkipType = typeof(SmokeSettings))]
    public async Task Create_ValidItem_RedirectsToIndexAndItemIsListed()
    {
        using var client = new AppClient();

        // CreateAsync posts the form and asserts the 302 to Index.
        await using var item = await CatalogItemScope.CreateAsync(client);

        var row = await CatalogUi.FindByNameAsync(client, item.Name);
        Assert.NotNull(row);
        Assert.Equal(item.Description, row.Description);
        Assert.Equal(item.BrandText, row.Brand);
        Assert.Equal(item.TypeText, row.Type);
        Assert.Contains(item.Price, row.Price);
        Assert.Equal("dummy.png", row.PictureFileName);
    }

    [Fact(Skip = "Razor UI: legacy only since the cutover (P2-9)", SkipUnless = nameof(SmokeSettings.IsLegacy), SkipType = typeof(SmokeSettings))]
    public async Task Create_EmptyName_ReturnsFormWithValidationErrorAndCreatesNothing()
    {
        using var client = new AppClient();
        var marker = CatalogUi.NewItemName();

        using var response = await CatalogUi.PostCreateAsync(client, (form, fields) =>
        {
            CatalogUi.FillValidItem(fields, marker, form.SelectOptions("CatalogBrandId")[0].Value, form.SelectOptions("CatalogTypeId")[0].Value);
            fields["Name"] = string.Empty;
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = HtmlPage.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), "/Catalog/Create");
        Assert.True(page.HasValidationError, "Expected a field validation error on the re-rendered form.");
        Assert.Null(await CatalogUi.FindAsync(client, r => r.Description == $"Smoke test item {marker}"));
    }

    [Fact(Skip = "Razor UI: legacy only since the cutover (P2-9)", SkipUnless = nameof(SmokeSettings.IsLegacy), SkipType = typeof(SmokeSettings))]
    public async Task Create_WithoutAntiforgeryToken_IsRejectedAndCreatesNothing()
    {
        // Fresh client that never loaded the form: no antiforgery cookie and no token field.
        using var client = new AppClient();
        var name = CatalogUi.NewItemName();
        var fields = new Dictionary<string, string>();
        CatalogUi.FillValidItem(fields, name, brandId: "1", typeId: "1");

        using var response = await client.PostFormAsync("/Catalog/Create", fields);

        // Legacy answers 500 (HttpAntiForgeryException); ASP.NET Core answers 400. Either way: rejected.
        Assert.True((int)response.StatusCode >= 400, $"Expected a rejection, got {(int)response.StatusCode}.");
        var created = await CatalogUi.FindByNameAsync(client, name);
        if (created is not null)
        {
            await CatalogItemScope.Adopt(created.Id, name).DisposeAsync();
        }

        Assert.Null(created);
    }
}
