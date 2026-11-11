using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using eShopMvc.Web.Infrastructure;

namespace eShopMvc.Web.IntegrationTests.Web;

/// <summary>Decision P2-6 catalog JSON API and P2-7 antiforgery, in mock mode.</summary>
public sealed class CatalogApiTests(MockModeAppFactory factory) : IClassFixture<MockModeAppFactory>
{
    private const string Items = "/api/v1/catalog/items";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetItems_ReturnsPascalCasePageWithBrandTypeAndPictureUri()
    {
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync($"{Items}?pageSize=3&pageIndex=1", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response, "application/json");
        var root = json.RootElement;
        Assert.Equal(["ActualPage", "Data", "ItemsPerPage", "TotalItems", "TotalPages"], PropertyNames(root));
        Assert.Equal(1, root.GetProperty("ActualPage").GetInt32());
        Assert.Equal(3, root.GetProperty("ItemsPerPage").GetInt32());
        var first = root.GetProperty("Data")[0];
        Assert.Equal(4, first.GetProperty("Id").GetInt32());
        Assert.Equal("http://localhost/items/4/pic", first.GetProperty("PictureUri").GetString());
        Assert.Equal(JsonValueKind.String, first.GetProperty("CatalogBrand").GetProperty("Brand").ValueKind);
        Assert.Equal(JsonValueKind.String, first.GetProperty("CatalogType").GetProperty("Type").ValueKind);
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageIndex=-1")]
    [InlineData("pageSize=2147483647&pageIndex=2")]
    public async Task GetItems_InvalidPaging_Returns400ProblemDetails(string query)
    {
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync($"{Items}?{query}", Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // Decision R-16
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetItem_ReturnsItemOr404ProblemDetails()
    {
        using var client = factory.CreateRawClient();

        using var found = await client.GetAsync($"{Items}/2", Token);
        using var missing = await client.GetAsync($"{Items}/999", Token);

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        using var json = await ReadJsonAsync(found, "application/json");
        Assert.Equal(2, json.RootElement.GetProperty("Id").GetInt32());
        Assert.Equal("http://localhost/items/2/pic", json.RootElement.GetProperty("PictureUri").GetString());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetBrandsAndTypes_ReturnPascalCaseLists()
    {
        using var client = factory.CreateRawClient();

        using var brands = await client.GetAsync("/api/v1/catalog/brands", Token);
        using var types = await client.GetAsync("/api/v1/catalog/types", Token);

        Assert.Equal(
            """[{"Id":1,"Brand":"Azure"},{"Id":2,"Brand":".NET"},{"Id":3,"Brand":"Visual Studio"},{"Id":4,"Brand":"SQL Server"},{"Id":5,"Brand":"Other"}]""",
            await brands.Content.ReadAsStringAsync(Token));
        Assert.StartsWith("""[{"Id":1,"Type":"Mug"},""", await types.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task Get_IssuesReadableStrictXsrfCookie()
    {
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync("/api/v1/catalog/types", Token);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith($"{AntiforgeryCookieFilter.CookieName}=", StringComparison.Ordinal));
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        // The HttpOnly cookie the token is paired with.
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("POST", Items)]
    [InlineData("PUT", Items + "/1")]
    [InlineData("DELETE", Items + "/1")]
    public async Task Writes_WithoutAntiforgeryToken_Return400AndChangeNothing(string method, string url)
    {
        using var client = factory.CreateRawClient();
        await GetXsrfTokenAsync(client); // cookies present, header missing

        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method != "DELETE")
        {
            request.Content = JsonContent.Create(Item(name: "no-token", id: 1));
        }
        using var response = await client.SendAsync(request, Token);
        using var item1 = await client.GetAsync($"{Items}/1", Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // Decision P2-7
        Assert.Equal(HttpStatusCode.OK, item1.StatusCode);
        Assert.DoesNotContain("no-token", await item1.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task Write_WithWrongToken_Returns400()
    {
        using var client = factory.CreateRawClient();
        await GetXsrfTokenAsync(client);

        using var response = await SendAsync(client, HttpMethod.Post, Items, "not-a-token", Item(name: "wrong-token"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateGetUpdateDelete_RoundTrip()
    {
        using var client = factory.CreateRawClient();
        var token = await GetXsrfTokenAsync(client);

        // Create: the body Id is ignored.
        using var created = await SendAsync(client, HttpMethod.Post, Items, token, Item(name: "api-created", id: 1));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = await ReadJsonAsync(created, "application/json");
        var id = createdJson.RootElement.GetProperty("Id").GetInt32();
        Assert.NotEqual(1, id);
        Assert.Equal($"http://localhost{Items}/{id}", created.Headers.Location?.ToString());
        Assert.Equal("Azure", createdJson.RootElement.GetProperty("CatalogBrand").GetProperty("Brand").GetString());
        Assert.Equal($"http://localhost/items/{id}/pic", createdJson.RootElement.GetProperty("PictureUri").GetString());

        // Update: brand and type change, PictureUri from the client is ignored.
        var update = Item(name: "api-updated", id: id, brandId: 2, typeId: 2);
        update["PictureUri"] = "http://evil.example/";
        using (var updated = await SendAsync(client, HttpMethod.Put, $"{Items}/{id}", token, update))
        {
            Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        }
        using (var read = await client.GetAsync($"{Items}/{id}", Token))
        {
            using var json = await ReadJsonAsync(read, "application/json");
            Assert.Equal("api-updated", json.RootElement.GetProperty("Name").GetString());
            Assert.Equal(".NET", json.RootElement.GetProperty("CatalogBrand").GetProperty("Brand").GetString());
            Assert.Equal(2, json.RootElement.GetProperty("CatalogTypeId").GetInt32());
            Assert.Equal($"http://localhost/items/{id}/pic", json.RootElement.GetProperty("PictureUri").GetString());
        }

        // Delete, then it's gone.
        using (var deleted = await SendAsync(client, HttpMethod.Delete, $"{Items}/{id}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }
        using var gone = await client.GetAsync($"{Items}/{id}", Token);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task Create_Invalid_Returns400WithPascalCaseFieldErrors()
    {
        using var client = factory.CreateRawClient();
        var token = await GetXsrfTokenAsync(client);
        var item = Item(name: "unused");
        item["Name"] = null;
        item["Price"] = 1000001m;
        item["PictureFileName"] = "../web.config"; // Decision R-9
        item["AvailableStock"] = -1;

        using var response = await SendAsync(client, HttpMethod.Post, Items, token, item);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ReadErrorsAsync(response);
        Assert.Equal(["AvailableStock", "Name", "PictureFileName", "Price"], errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("The field Stock must be between 0 and 10 million.", Assert.Single(errors["AvailableStock"]));
    }

    [Theory]
    [InlineData("1000000.4")]
    [InlineData("2147483648")]
    [InlineData("99999999999")]
    public async Task Create_PriceAboveTheRange_Returns400WithTheRangeMessage_NotA500(string price)
    {
        // Decision P2-20 (legacy's int range: 500 above int.MaxValue, 1000000.4 accepted).
        using var client = factory.CreateRawClient();
        var token = await GetXsrfTokenAsync(client);
        var item = Item(name: "too-expensive");
        item["Price"] = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);

        using var response = await SendAsync(client, HttpMethod.Post, Items, token, item);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ReadErrorsAsync(response);
        Assert.Equal("The field Price must be between 0 and 1000000.", Assert.Single(Assert.Single(errors).Value));
    }

    [Fact]
    public async Task Create_UnknownBrandAndType_Returns400FieldErrors()
    {
        using var client = factory.CreateRawClient();
        var token = await GetXsrfTokenAsync(client);

        using var response = await SendAsync(client, HttpMethod.Post, Items, token, Item(name: "unknown-refs", brandId: 999, typeId: 999));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // Decision P2-13
        var errors = await ReadErrorsAsync(response);
        Assert.Equal(["CatalogBrandId", "CatalogTypeId"], errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Update_IdMismatchOrMissing_Returns400Or404()
    {
        using var client = factory.CreateRawClient();
        var token = await GetXsrfTokenAsync(client);

        using var mismatch = await SendAsync(client, HttpMethod.Put, $"{Items}/1", token, Item(name: "mismatch", id: 2));
        using var missing = await SendAsync(client, HttpMethod.Put, $"{Items}/999", token, Item(name: "missing", id: 999));

        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        using (var problem = await ReadJsonAsync(mismatch, "application/problem+json"))
        {
            Assert.Contains("must match", problem.RootElement.GetProperty("detail").GetString());
        }
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Delete_Missing_Returns404()
    {
        using var client = factory.CreateRawClient();
        var token = await GetXsrfTokenAsync(client);

        using var response = await SendAsync(client, HttpMethod.Delete, $"{Items}/999", token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // Decision R-15
    }

    private static Dictionary<string, object?> Item(string name, int id = 0, int brandId = 1, int typeId = 1) => new()
    {
        ["Id"] = id,
        ["Name"] = name,
        ["Description"] = "integration test",
        ["Price"] = 12.34m,
        ["PictureFileName"] = "dummy.png",
        ["CatalogBrandId"] = brandId,
        ["CatalogTypeId"] = typeId,
        ["AvailableStock"] = 7,
        ["RestockThreshold"] = 2,
        ["MaxStockThreshold"] = 20,
    };

    /// <summary>A GET issues the token cookie (the client keeps the cookies); returns the token for the header.</summary>
    private static async Task<string> GetXsrfTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/catalog/brands", Token);
        var prefix = $"{AntiforgeryCookieFilter.CookieName}=";
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(prefix, StringComparison.Ordinal));
        return Uri.UnescapeDataString(cookie[prefix.Length..cookie.IndexOf(';')]);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add(AntiforgeryCookieFilter.HeaderName, token);
        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }
        return client.SendAsync(request, Token);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, string mediaType)
    {
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
    }

    private static async Task<Dictionary<string, string[]>> ReadErrorsAsync(HttpResponseMessage response)
    {
        using var json = await ReadJsonAsync(response, "application/problem+json");
        return json.RootElement.GetProperty("errors").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(e => e.GetString()!).ToArray());
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal);
}
