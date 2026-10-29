using System.Net;
using System.Text.Json;
using eShopMvc.SmokeTests.Infrastructure;

namespace eShopMvc.SmokeTests.Api;

/// <summary>
/// <c>/api/v1/catalog</c>, the JSON API for the React UI (Decisions P2-6, P2-7). New app only: legacy has no such API.
/// Items are created with the <c>smoke-</c> prefix and deleted again; seeded items are only read.
/// </summary>
[Trait("Category", "Api")]
public sealed class CatalogApiTests
{
    private const string Items = "/api/v1/catalog/items";

    [Fact(Skip = "New-app API (P2-6)", SkipUnless = nameof(SmokeSettings.IsNew), SkipType = typeof(SmokeSettings))]
    public async Task GetItems_ReturnsPascalCasePage()
    {
        using var client = new AppClient();

        using var response = await client.GetJsonAsync($"{Items}?pageSize=2&pageIndex=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await JsonShape.ReadJsonAsync(response);
        Assert.Equal(["ActualPage", "Data", "ItemsPerPage", "TotalItems", "TotalPages"],
            json.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        var first = json.RootElement.GetProperty("Data")[0];
        Assert.EndsWith($"/items/{first.GetProperty("Id").GetInt32()}/pic", first.GetProperty("PictureUri").GetString());
    }

    [Fact(Skip = "New-app API (P2-6)", SkipUnless = nameof(SmokeSettings.IsNew), SkipType = typeof(SmokeSettings))]
    public async Task CreateGetUpdateDelete_RoundTrip()
    {
        using var client = new AppClient();
        var (brandIds, typeIds) = await BrandAndTypeIdsAsync(client);
        var name = CatalogUi.NewItemName();
        int? id = null;

        try
        {
            using (var created = await client.SendJsonAsync(HttpMethod.Post, Items, Item(name, brandIds[0], typeIds[0])))
            {
                Assert.Equal(HttpStatusCode.Created, created.StatusCode);
                using var json = await JsonShape.ReadJsonAsync(created);
                id = json.RootElement.GetProperty("Id").GetInt32();
                Assert.EndsWith($"{Items}/{id}", created.Headers.Location?.ToString());
            }

            // Change brand and type too: the database service updates the tracked item (P2-6).
            var update = Item(name + "-u", brandIds[^1], typeIds[^1], price: 56.78m);
            update["Id"] = id;
            using (var updated = await client.SendJsonAsync(HttpMethod.Put, $"{Items}/{id}", update))
            {
                Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
            }

            using (var read = await client.GetJsonAsync($"{Items}/{id}"))
            {
                Assert.Equal(HttpStatusCode.OK, read.StatusCode);
                using var json = await JsonShape.ReadJsonAsync(read);
                var item = json.RootElement;
                Assert.Equal(name + "-u", item.GetProperty("Name").GetString());
                Assert.Equal(56.78m, item.GetProperty("Price").GetDecimal());
                Assert.Equal(brandIds[^1], item.GetProperty("CatalogBrand").GetProperty("Id").GetInt32());
                Assert.Equal(typeIds[^1], item.GetProperty("CatalogType").GetProperty("Id").GetInt32());
            }

            using (var deleted = await client.SendJsonAsync(HttpMethod.Delete, $"{Items}/{id}"))
            {
                Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            }
            using var gone = await client.GetJsonAsync($"{Items}/{id}");
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
            id = null;
        }
        finally
        {
            if (id != null)
            {
                using var cleanup = await client.SendJsonAsync(HttpMethod.Delete, $"{Items}/{id}");
            }
        }
    }

    [Fact(Skip = "New-app API (P2-7)", SkipUnless = nameof(SmokeSettings.IsNew), SkipType = typeof(SmokeSettings))]
    public async Task Create_WithoutAntiforgeryHeader_Returns400AndCreatesNothing()
    {
        using var client = new AppClient();
        var (brandIds, typeIds) = await BrandAndTypeIdsAsync(client);
        var name = CatalogUi.NewItemName();

        using var response = await client.SendJsonAsync(HttpMethod.Post, Items, Item(name, brandIds[0], typeIds[0]), withXsrfHeader: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await CatalogUi.FindByNameAsync(client, name));
    }

    [Fact(Skip = "New-app API (P2-6, P2-13)", SkipUnless = nameof(SmokeSettings.IsNew), SkipType = typeof(SmokeSettings))]
    public async Task Create_Invalid_Returns400WithFieldErrors()
    {
        using var client = new AppClient();
        await BrandAndTypeIdsAsync(client);
        var item = Item(CatalogUi.NewItemName(), brandId: int.MaxValue, typeId: int.MaxValue);
        item["PictureFileName"] = "../x.png";

        using var response = await client.SendJsonAsync(HttpMethod.Post, Items, item);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["PictureFileName"],
            json.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name));
        // Annotation errors stop the request before the brand/type check, so only PictureFileName is reported.
    }

    private static async Task<(IReadOnlyList<int> BrandIds, IReadOnlyList<int> TypeIds)> BrandAndTypeIdsAsync(AppClient client)
    {
        // These GETs also issue the antiforgery cookies.
        return (await IdsAsync(client, "/api/v1/catalog/brands"), await IdsAsync(client, "/api/v1/catalog/types"));

        static async Task<IReadOnlyList<int>> IdsAsync(AppClient client, string url)
        {
            using var response = await client.GetJsonAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await JsonShape.ReadJsonAsync(response);
            return json.RootElement.EnumerateArray().Select(e => e.GetProperty("Id").GetInt32()).ToList();
        }
    }

    private static Dictionary<string, object?> Item(string name, int brandId, int typeId, decimal price = 12.34m) => new()
    {
        ["Name"] = name,
        ["Description"] = $"Smoke test item {name}",
        ["Price"] = price,
        ["PictureFileName"] = "dummy.png",
        ["CatalogBrandId"] = brandId,
        ["CatalogTypeId"] = typeId,
        ["AvailableStock"] = 7,
        ["RestockThreshold"] = 2,
        ["MaxStockThreshold"] = 20,
    };
}
