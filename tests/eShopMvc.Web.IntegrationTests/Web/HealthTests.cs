using System.Net;

namespace eShopMvc.Web.IntegrationTests.Web;

public sealed class HealthTests(MockModeAppFactory factory) : IClassFixture<MockModeAppFactory>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")] // mock mode: no database check
    public async Task Health_ReturnsHealthyWithoutDetailsOrCookies(string path)
    {
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync(path, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Token));
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }
}
