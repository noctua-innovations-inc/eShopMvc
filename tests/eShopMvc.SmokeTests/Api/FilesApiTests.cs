using System.Net;
using eShopMvc.SmokeTests.Infrastructure;

namespace eShopMvc.SmokeTests.Api;

/// <summary>
/// Decision R-1: <c>/api/files</c> returned a BinaryFormatter payload, which .NET 10 cannot produce.
/// The new app answers <c>410 Gone</c> there and serves JSON at <c>/api/v1/files</c>.
/// Each case runs against one target and is skipped on the other (ESHOP_TARGET).
/// </summary>
[Trait("Category", "Api")]
[Trait("Category", "Target")]
public sealed class FilesApiTests
{
    // BinaryFormatter SerializationHeaderRecord: record type 0, root id 1, header id -1, version 1.0.
    private static readonly byte[] BinaryFormatterHeader = [0x00, 0x01, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF];

    [Fact(Skip = "Legacy-only behavior (R-1)", SkipUnless = nameof(SmokeSettings.IsLegacy), SkipType = typeof(SmokeSettings))]
    public async Task Files_Legacy_ReturnsBinaryFormatterPayload()
    {
        using var client = new AppClient();

        using var response = await client.GetAsync("/api/files");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.True(body.Length > BinaryFormatterHeader.Length, "Expected a BinaryFormatter payload.");
        Assert.Equal(BinaryFormatterHeader, body.Take(BinaryFormatterHeader.Length));
    }

    [Fact(Skip = "New-app behavior (R-1)", SkipUnless = nameof(SmokeSettings.IsNew), SkipType = typeof(SmokeSettings))]
    public async Task Files_New_Returns410ProblemDetailsWithSuccessorLink()
    {
        using var client = new AppClient();

        using var response = await client.GetJsonAsync("/api/files");

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.TryGetValues("Link", out var links), "Expected a Link header.");
        Assert.Contains(links!, l => l.Contains("</api/v1/files>; rel=\"successor-version\"", StringComparison.Ordinal));
        Assert.False(response.Headers.Location is not null, "R-1: no redirect.");
    }

    [Fact(Skip = "Legacy-only behavior (R-1)", SkipUnless = nameof(SmokeSettings.IsLegacy), SkipType = typeof(SmokeSettings))]
    public async Task FilesV1_Legacy_DoesNotExist()
    {
        using var client = new AppClient();

        using var response = await client.GetJsonAsync("/api/v1/files");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(Skip = "New-app behavior (R-1)", SkipUnless = nameof(SmokeSettings.IsNew), SkipType = typeof(SmokeSettings))]
    public async Task FilesV1_New_ReturnsPascalCaseBrandDtoArray()
    {
        using var client = new AppClient();

        using var response = await client.GetJsonAsync("/api/v1/files");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await JsonShape.ReadJsonAsync(response);
        JsonShape.AssertBrandArray(json.RootElement);
    }
}
