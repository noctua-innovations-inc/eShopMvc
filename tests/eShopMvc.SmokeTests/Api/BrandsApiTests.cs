using System.Net;
using eShopMvc.SmokeTests.Infrastructure;

namespace eShopMvc.SmokeTests.Api;

/// <summary><c>/api/brands</c> (Web API 2 in legacy).</summary>
[Trait("Category", "Api")]
public sealed class BrandsApiTests
{
    private const int MissingId = int.MaxValue;

    [Fact]
    public async Task GetBrands_ReturnsPascalCaseBrandArray()
    {
        using var client = new AppClient();

        using var response = await client.GetJsonAsync("/api/brands");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await JsonShape.ReadJsonAsync(response);
        JsonShape.AssertBrandArray(json.RootElement);
    }

    [Fact]
    public async Task GetBrandById_ReturnsThatBrand()
    {
        using var client = new AppClient();
        var id = await AnyBrandIdAsync(client);

        using var response = await client.GetJsonAsync($"/api/brands/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await JsonShape.ReadJsonAsync(response);
        Assert.Equal(id, JsonShape.AssertBrand(json.RootElement));
    }

    [Fact]
    public async Task GetBrandById_Unknown_Returns404()
    {
        using var client = new AppClient();

        using var response = await client.GetJsonAsync($"/api/brands/{MissingId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBrand_Existing_Returns200ButDoesNotDelete()
    {
        using var client = new AppClient();
        var id = await AnyBrandIdAsync(client);

        using (var response = await client.DeleteAsync($"/api/brands/{id}"))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Demo-only endpoint: the brand is still there afterwards.
        using var after = await client.GetJsonAsync($"/api/brands/{id}");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [Fact]
    public async Task DeleteBrand_Unknown_Returns404()
    {
        using var client = new AppClient();

        using var response = await client.DeleteAsync($"/api/brands/{MissingId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<int> AnyBrandIdAsync(AppClient client)
    {
        using var response = await client.GetJsonAsync("/api/brands");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await JsonShape.ReadJsonAsync(response);
        return JsonShape.AssertBrandArray(json.RootElement)[0];
    }
}
