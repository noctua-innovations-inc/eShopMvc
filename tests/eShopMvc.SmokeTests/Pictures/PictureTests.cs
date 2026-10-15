using System.Net;
using eShopMvc.SmokeTests.Infrastructure;

namespace eShopMvc.SmokeTests.Pictures;

/// <summary><c>GET /items/{catalogItemId:int}/pic</c>.</summary>
[Trait("Category", "Pictures")]
public sealed class PictureTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public async Task Picture_OfListedItem_HasContentTypeMatchingItsFileName()
    {
        using var client = new AppClient();
        var row = CatalogUi.Rows(await client.GetPageAsync("/Catalog/Index?pageSize=1&pageIndex=0")).Single();

        using var response = await client.GetAsync(row.ThumbnailSrc);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var expected = ExpectedMediaType(Path.GetExtension(row.PictureFileName));
        Assert.Equal(expected, response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(body);
        if (expected == "image/png")
        {
            Assert.Equal(PngSignature, body.Take(PngSignature.Length));
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task Picture_NonPositiveId_Returns400(string id)
    {
        using var client = new AppClient();

        using var response = await client.GetAsync($"/items/{id}/pic");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("2147483647")] // no such item
    [InlineData("abc")] // doesn't match the {catalogItemId:int} route
    public async Task Picture_UnknownItemOrNonIntegerId_Returns404(string id)
    {
        using var client = new AppClient();

        using var response = await client.GetAsync($"/items/{id}/pic");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Mirrors PicController.GetImageMimeTypeFromImageFileExtension in the legacy app.
    private static string ExpectedMediaType(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".bmp" => "image/bmp",
        ".tiff" => "image/tiff",
        ".wmf" => "image/wmf",
        ".jp2" => "image/jp2",
        ".svg" => "image/svg+xml",
        _ => "application/octet-stream",
    };
}
