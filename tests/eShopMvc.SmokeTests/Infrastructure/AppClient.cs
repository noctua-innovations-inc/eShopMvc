using System.Net;

namespace eShopMvc.SmokeTests.Infrastructure;

/// <summary>
/// An HTTP client that behaves like a single browser session against the app under test:
/// its own cookie jar (session and antiforgery cookies), no automatic redirects (so 302s can be asserted).
/// </summary>
public sealed class AppClient : IDisposable
{
    private const string BrowserAccept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

    private readonly HttpClient http;

    public AppClient()
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AllowAutoRedirect = false,
        };

        http = new HttpClient(handler)
        {
            BaseAddress = SmokeSettings.BaseUrl,
            // The first request after the legacy app starts creates and seeds the database.
            Timeout = TimeSpan.FromSeconds(120),
        };
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>GET with a browser-like Accept header.</summary>
    public Task<HttpResponseMessage> GetAsync(string url) => SendAsync(HttpMethod.Get, url, BrowserAccept);

    /// <summary>GET with <c>Accept: application/json</c>.</summary>
    public Task<HttpResponseMessage> GetJsonAsync(string url) => SendAsync(HttpMethod.Get, url, "application/json");

    public Task<HttpResponseMessage> DeleteAsync(string url) => SendAsync(HttpMethod.Delete, url, "application/json");

    /// <summary>GETs an HTML page and asserts it returned 200 text/html.</summary>
    public async Task<HtmlPage> GetPageAsync(string url)
    {
        using var response = await GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        return HtmlPage.Parse(await response.Content.ReadAsStringAsync(Token), url);
    }

    /// <summary>POSTs <c>application/x-www-form-urlencoded</c> fields, as a browser form submit does.</summary>
    public Task<HttpResponseMessage> PostFormAsync(string url, IEnumerable<KeyValuePair<string, string>> fields)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(fields),
        };
        request.Headers.TryAddWithoutValidation("Accept", BrowserAccept);
        return http.SendAsync(request, Token);
    }

    public void Dispose() => http.Dispose();

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string accept)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("Accept", accept);
        return http.SendAsync(request, Token);
    }
}
