using System.Net;
using eShopMvc.SmokeTests.Infrastructure;

namespace eShopMvc.SmokeTests.Api;

[Trait("Category", "Api")]
public sealed class ApiRootTests
{
    /// <summary>
    /// Decision R-8: legacy <c>CatalogController2</c> ([Route("api")]) is never discovered, so <c>/api</c> is 404.
    /// It is not ported, so the new app must return 404 too. Same expectation on both targets.
    /// </summary>
    [Fact]
    public async Task ApiRoot_Returns404()
    {
        using var client = new AppClient();

        using var response = await client.GetAsync("/api");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
