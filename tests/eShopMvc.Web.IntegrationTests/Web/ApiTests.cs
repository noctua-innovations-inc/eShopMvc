using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using eShopMvc.Web.Models;
using eShopMvc.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace eShopMvc.Web.IntegrationTests.Web;

public sealed class ApiTests(MockModeAppFactory factory) : IClassFixture<MockModeAppFactory>
{
    private readonly HttpClient client = factory.CreateRawClient();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Factory_RunsInMockMode_SoTheAppDatabaseIsNeverTouched()
    {
        using var scope = factory.Services.CreateScope();

        Assert.IsType<CatalogServiceMock>(scope.ServiceProvider.GetRequiredService<ICatalogService>());
        Assert.Null(scope.ServiceProvider.GetService<CatalogDBContext>());
    }

    [Fact]
    public async Task GetBrands_ReturnsPascalCaseSeedBrands()
    {
        using var response = await client.GetAsync("/api/brands", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            """[{"Id":1,"Brand":"Azure"},{"Id":2,"Brand":".NET"},{"Id":3,"Brand":"Visual Studio"},{"Id":4,"Brand":"SQL Server"},{"Id":5,"Brand":"Other"}]""",
            await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task GetBrands_AcceptXml_StillReturnsJson()
    {
        // Decision R-7: no XML formatter.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/brands");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

        using var response = await client.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetBrandById_ReturnsBrandOr404()
    {
        using var found = await client.GetAsync("/api/brands/2", Token);
        using var missing = await client.GetAsync("/api/brands/999", Token);

        Assert.Equal("""{"Id":2,"Brand":".NET"}""", await found.Content.ReadAsStringAsync(Token));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task DeleteBrand_Returns200EmptyOr404_AndDeletesNothing()
    {
        using var existing = await client.DeleteAsync("/api/brands/1", Token);
        using var missing = await client.DeleteAsync("/api/brands/999", Token);
        using var after = await client.GetAsync("/api/brands/1", Token);

        Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
        Assert.Empty(await existing.Content.ReadAsByteArrayAsync(Token));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [Fact]
    public async Task FilesV1_ReturnsPascalCaseBrandDtos()
    {
        using var response = await client.GetAsync("/api/v1/files", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("""[{"Id":1,"Brand":"Azure"},""", await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task Files_Returns410ProblemDetailsWithSuccessorLinkAndNoRedirect()
    {
        using var response = await client.GetAsync("/api/files", Token);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(["</api/v1/files>; rel=\"successor-version\""], response.Headers.GetValues("Link"));
        Assert.Null(response.Headers.Location);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal(410, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("/api/v1/files", problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ApiRoot_Returns404()
    {
        // Decision R-8: CatalogController2 is not ported.
        using var response = await client.GetAsync("/api", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
