using System.Text.Json;

namespace eShopMvc.SmokeTests.Api;

internal static class JsonShape
{
    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return JsonDocument.Parse(json);
    }

    /// <summary>
    /// Asserts a brand object has exactly the PascalCase properties <c>Id</c> (number) and <c>Brand</c> (string)
    /// (Decision R-7: PascalCase for all API responses). Returns the Id.
    /// </summary>
    public static int AssertBrand(JsonElement brand)
    {
        Assert.Equal(JsonValueKind.Object, brand.ValueKind);
        Assert.Equal(["Brand", "Id"], brand.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(JsonValueKind.Number, brand.GetProperty("Id").ValueKind);
        Assert.Equal(JsonValueKind.String, brand.GetProperty("Brand").ValueKind);
        return brand.GetProperty("Id").GetInt32();
    }

    /// <summary>Asserts a non-empty array of brand objects and returns their ids.</summary>
    public static IReadOnlyList<int> AssertBrandArray(JsonElement root)
    {
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.NotEqual(0, root.GetArrayLength());
        return root.EnumerateArray().Select(AssertBrand).ToList();
    }
}
