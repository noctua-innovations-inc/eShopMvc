using System.Net;
using eShopMvc.Web.Models;
using eShopMvc.Web.Services;
using eShopMvc.Web.ViewModel;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace eShopMvc.Web.IntegrationTests.Web;

public sealed class PictureTests(MockModeAppFactory factory) : IClassFixture<MockModeAppFactory>
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Picture_OfSeededItem_IsThePngFromPics()
    {
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync("/items/1/pic", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsByteArrayAsync(Token);
        Assert.Equal(PngSignature, body.Take(PngSignature.Length));
    }

    [Theory]
    [InlineData("0", HttpStatusCode.BadRequest)]
    [InlineData("-1", HttpStatusCode.BadRequest)]
    [InlineData("999", HttpStatusCode.NotFound)]
    [InlineData("abc", HttpStatusCode.NotFound)] // doesn't match {catalogItemId:int}
    public async Task Picture_InvalidOrUnknownId_ReturnsLegacyStatus(string id, HttpStatusCode expected)
    {
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync($"/items/{id}/pic", Token);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("/Pics/1.png")]
    [InlineData("/pics/dummy.png")]
    public async Task PicsFolder_IsNotServedDirectly(string path)
    {
        // Decision R-9: legacy IIS also served ~/Pics as static files; here only /items/{id}/pic serves them.
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync(path, Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("..", "appsettings.json")]
    [InlineData("..", "..", "eShopMvc.slnx")]
    public async Task Picture_StoredNameOutsidePics_Returns404(params string[] pathParts)
    {
        // Decision R-9, defense in depth: validation stops such names on Create/Edit, so a stub
        // service stands in for data that got into the database some other way.
        var fileName = Path.Combine(pathParts);
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<ICatalogService>(new SingleItemService(fileName))));
        using var client = app.CreateClient();

        using var response = await client.GetAsync("/items/1/pic", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class SingleItemService(string pictureFileName) : ICatalogService
    {
        public CatalogItem? FindCatalogItem(int id) =>
            id == 1 ? new CatalogItem { Id = 1, Name = "x", PictureFileName = pictureFileName } : null;

        public IEnumerable<CatalogBrand> GetCatalogBrands() => [];
        public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex) => throw new NotSupportedException();
        public IEnumerable<CatalogType> GetCatalogTypes() => [];
        public void CreateCatalogItem(CatalogItem catalogItem) => throw new NotSupportedException();
        public void UpdateCatalogItem(CatalogItem catalogItem) => throw new NotSupportedException();
        public void RemoveCatalogItem(CatalogItem catalogItem) => throw new NotSupportedException();
    }
}
