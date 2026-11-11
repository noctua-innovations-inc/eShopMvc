using System.Net;
using System.Text.Json;

namespace eShopMvc.Web.IntegrationTests.Web;

/// <summary>Decision P2-8: <c>GET /api/v1/session</c> for the React footer (R-11 values).</summary>
public sealed class SessionApiTests(MockModeAppFactory factory) : IClassFixture<MockModeAppFactory>
{
    private const string Session = "/api/v1/session";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_StartsASessionAndReturnsPascalCaseLegacyFormattedValues()
    {
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync(Session, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Session=", StringComparison.Ordinal));
        Assert.True(response.Headers.CacheControl?.NoStore);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal(["MachineName", "SessionStartTime"], json.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(Environment.MachineName, json.RootElement.GetProperty("MachineName").GetString());
        Assert.Matches(@"^\d{1,2}/\d{1,2}/\d{4} \d{1,2}:\d{2}:\d{2} (AM|PM)$", json.RootElement.GetProperty("SessionStartTime").GetString());
    }

    [Fact]
    public async Task Get_IsStableWithinTheSession_AndNewClientsGetTheirOwn()
    {
        using var client = factory.CreateClient();
        using var other = factory.CreateClient();

        var first = await GetSessionAsync(client);
        await Task.Delay(1100, Token); // the start time has one-second resolution
        var second = await GetSessionAsync(client);
        var otherSession = await GetSessionAsync(other);

        Assert.Equal(first, second);
        Assert.NotEqual(first, otherSession);
    }

    [Fact]
    public async Task OtherApis_DoNotStartASession()
    {
        // Decision R-11: legacy Web API had no session; only this endpoint starts one (P2-8).
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync("/api/brands", Token);

        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    /// <summary>The session values as the footer prints them: "{MachineName}, {SessionStartTime}".</summary>
    private static async Task<string> GetSessionAsync(HttpClient client)
    {
        using var json = JsonDocument.Parse(await client.GetStringAsync(Session, Token));
        return $"{json.RootElement.GetProperty("MachineName").GetString()}, {json.RootElement.GetProperty("SessionStartTime").GetString()}";
    }
}
