using System.Net;
using eShopMvc.SmokeTests.Infrastructure;

namespace eShopMvc.SmokeTests.Api;

/// <summary><c>/api/v1/session</c>, the React footer's session values (Decision P2-8). New app only.</summary>
[Trait("Category", "Api")]
public sealed class SessionApiTests
{
    [Fact(Skip = "New-app API (P2-8)", SkipUnless = nameof(SmokeSettings.IsNew), SkipType = typeof(SmokeSettings))]
    public async Task Get_ReturnsLegacyFormattedValues_SameAsTheRazorFooter()
    {
        using var client = new AppClient();

        var first = await GetSessionAsync(client);
        var page = await client.GetPageAsync("/");
        var second = await GetSessionAsync(client);

        Assert.Matches(@"^.+, \d{1,2}/\d{1,2}/\d{4} \d{1,2}:\d{2}:\d{2} (AM|PM)$", first);
        Assert.Equal(first, page.Single("footer small").TextContent.Trim());
        Assert.Equal(first, second);
    }

    /// <summary>The session values as the footer prints them: "{MachineName}, {SessionStartTime}".</summary>
    private static async Task<string> GetSessionAsync(AppClient client)
    {
        using var response = await client.GetJsonAsync("/api/v1/session");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await JsonShape.ReadJsonAsync(response);
        Assert.Equal(["MachineName", "SessionStartTime"], json.RootElement.EnumerateObject().Select(p => p.Name));
        return $"{json.RootElement.GetProperty("MachineName").GetString()}, {json.RootElement.GetProperty("SessionStartTime").GetString()}";
    }
}
