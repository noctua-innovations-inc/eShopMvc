using System.Net;
using System.Text.Json;
using eShopMvc.SmokeTests.Api;

namespace eShopMvc.SmokeTests.Infrastructure;

/// <summary>A catalog item as <c>/api/v1/catalog/items</c> returns it (the parts the smoke tests use).</summary>
public sealed record ApiCatalogItem(int Id, string Name, string PictureFileName, string PictureUri);

/// <summary>
/// Reads and writes catalog items through the JSON API (Decision P2-6), the way the React UI does.
/// New app only: since the cutover (P2-9) it has no HTML forms; <see cref="CatalogUi"/> is for legacy.
/// </summary>
public static class CatalogApi
{
    public const string Items = "/api/v1/catalog/items";

    private const int ScanPageSize = 100;

    /// <summary>Reads every page of the catalog. Like any GET here, it also issues the antiforgery cookies.</summary>
    public static async Task<IReadOnlyList<ApiCatalogItem>> AllItemsAsync(AppClient client)
    {
        var all = new List<ApiCatalogItem>();
        for (var pageIndex = 0; pageIndex < 1000; pageIndex++)
        {
            using var response = await client.GetJsonAsync($"{Items}?pageSize={ScanPageSize}&pageIndex={pageIndex}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await JsonShape.ReadJsonAsync(response);
            var data = json.RootElement.GetProperty("Data");
            all.AddRange(data.EnumerateArray().Select(Read));
            if (data.GetArrayLength() < ScanPageSize)
            {
                break;
            }
        }

        return all;
    }

    public static async Task<ApiCatalogItem?> FindByNameAsync(AppClient client, string name) =>
        (await AllItemsAsync(client)).FirstOrDefault(i => i.Name == name);

    /// <summary>The first brand and the first type of the forms' drop-downs.</summary>
    public static async Task<((int Id, string Text) Brand, (int Id, string Text) Type)> FirstBrandAndTypeAsync(AppClient client)
    {
        return (await FirstAsync("/api/v1/catalog/brands", "Brand"), await FirstAsync("/api/v1/catalog/types", "Type"));

        async Task<(int, string)> FirstAsync(string url, string textProperty)
        {
            using var response = await client.GetJsonAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await JsonShape.ReadJsonAsync(response);
            var first = json.RootElement[0];
            return (first.GetProperty("Id").GetInt32(), first.GetProperty(textProperty).GetString() ?? string.Empty);
        }
    }

    /// <summary>The body of a recognisable, valid item: the values <see cref="CatalogUi.FillValidItem"/> uses.</summary>
    public static Dictionary<string, object?> ValidItem(string name, int brandId, int typeId) => new()
    {
        ["Name"] = name,
        ["Description"] = $"Smoke test item {name}",
        ["Price"] = 12.34m,
        ["PictureFileName"] = "dummy.png",
        ["CatalogBrandId"] = brandId,
        ["CatalogTypeId"] = typeId,
        ["AvailableStock"] = 7,
        ["RestockThreshold"] = 2,
        ["MaxStockThreshold"] = 20,
    };

    /// <summary>POSTs an item and returns the id the server gave it.</summary>
    public static async Task<int> CreateAsync(AppClient client, Dictionary<string, object?> item)
    {
        using var response = await client.SendJsonAsync(HttpMethod.Post, Items, item);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await JsonShape.ReadJsonAsync(response);
        return json.RootElement.GetProperty("Id").GetInt32();
    }

    /// <summary>Deletes an item. Returns false if it no longer exists.</summary>
    public static async Task<bool> DeleteAsync(AppClient client, int id)
    {
        // The GET issues the antiforgery cookies if this client has none yet.
        using (var probe = await client.GetJsonAsync($"{Items}/{id}"))
        {
            if (probe.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        using var response = await client.SendJsonAsync(HttpMethod.Delete, $"{Items}/{id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return true;
    }

    private static ApiCatalogItem Read(JsonElement item) => new(
        item.GetProperty("Id").GetInt32(),
        item.GetProperty("Name").GetString() ?? string.Empty,
        item.GetProperty("PictureFileName").GetString() ?? string.Empty,
        item.GetProperty("PictureUri").GetString() ?? string.Empty);
}
