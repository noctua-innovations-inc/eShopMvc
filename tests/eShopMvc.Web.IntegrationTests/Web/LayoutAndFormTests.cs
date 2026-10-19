using System.Net;
using System.Text.RegularExpressions;

namespace eShopMvc.Web.IntegrationTests.Web;

public sealed partial class LayoutAndFormTests(MockModeAppFactory factory) : IClassFixture<MockModeAppFactory>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Footer_ShowsMachineNameAndLegacyFormattedSessionStart_StableWithinTheSession()
    {
        // Decision R-11. The factory client keeps cookies, so both requests share one session.
        using var client = factory.CreateClient();

        var first = Footer(await client.GetStringAsync("/", Token));
        var second = Footer(await client.GetStringAsync("/Catalog/Index?pageIndex=1", Token));

        Assert.Matches(@"^" + Regex.Escape(Environment.MachineName) + @", \d{1,2}/\d{1,2}/\d{4} \d{1,2}:\d{2}:\d{2} (AM|PM)$", first);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Api_DoesNotStartASession()
    {
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync("/api/brands", Token);

        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Layout_EveryReferencedStylesheetScriptAndImageLoads()
    {
        using var client = factory.CreateRawClient();
        var html = await client.GetStringAsync("/Catalog/Create", Token);

        var urls = AssetUrl().Matches(html).Select(m => m.Groups["url"].Value).Distinct().ToList();

        Assert.Contains("/lib/bootstrap/dist/css/bootstrap.min.css", urls);
        Assert.Contains("/lib/jquery-validation-unobtrusive/dist/jquery.validate.unobtrusive.min.js", urls);
        foreach (var url in urls)
        {
            using var response = await client.GetAsync(url, Token);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode} {url}");
        }
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Catalog/Create")]
    public async Task Layout_LoadsNoModernizrOrRespond(string path)
    {
        // Decision R-18: legacy loaded modernizr-2.6.2.js, modernizr-2.8.3.js and respond.js.
        using var client = factory.CreateRawClient();

        var html = await client.GetStringAsync(path, Token);

        var scripts = Regex.Matches(html, "<script[^>]*src=\"(?<src>[^\"]+)\"").Select(m => m.Groups["src"].Value).ToList();
        Assert.NotEmpty(scripts);
        Assert.DoesNotContain(scripts, s => s.Contains("modernizr", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(scripts, s => s.Contains("respond", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("modernizr", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeletePost_MissingIdWithValidAntiforgeryToken_Returns404AndDeletesNothing()
    {
        // Decision R-15 end to end (legacy: 500 in database mode).
        using var client = factory.CreateClient();
        var form = await client.GetStringAsync("/Catalog/Delete/1", Token);
        var token = TokenValue().Match(form).Groups["token"].Value;
        Assert.NotEmpty(token);

        using var response = await client.PostAsync("/Catalog/Delete/999999",
            new FormUrlEncodedContent([new("__RequestVerificationToken", token)]), Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var stillThere = await client.GetAsync("/Catalog/Details/1", Token);
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task CssReferencedFontsAndImages_Load()
    {
        // ../fonts and ../images from wwwroot/css, matched case-sensitively (R-6).
        using var client = factory.CreateRawClient();
        foreach (var css in new[] { "/css/base.css", "/css/site.css" })
        {
            var text = await client.GetStringAsync(css, Token);
            foreach (var path in CssUrl().Matches(text).Select(m => m.Groups["path"].Value).Distinct())
            {
                using var response = await client.GetAsync("/" + path, Token);
                Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode} /{path} (from {css})");
            }
        }
    }

    [Theory]
    [InlineData("/Catalog/Create")]
    [InlineData("/Catalog/Edit/1")]
    [InlineData("/Catalog/Delete/1")]
    public async Task Forms_HaveExactlyOneAntiforgeryToken(string path)
    {
        using var client = factory.CreateRawClient();

        var html = await client.GetStringAsync(path, Token);

        Assert.Single(Regex.Matches(html, "name=\"__RequestVerificationToken\""));
    }

    [Fact]
    public async Task EditPost_Invalid_ReRendersWithThePicture()
    {
        // Decision R-24: legacy re-rendered the invalid Edit form without PictureUri (broken image).
        using var client = factory.CreateClient();
        var form = await client.GetStringAsync("/Catalog/Edit/2", Token);
        var token = TokenValue().Match(form).Groups["token"].Value;

        using var response = await client.PostAsync("/Catalog/Edit/2", new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", token), new("Id", "2"), new("Name", "x"), new("Price", "not-a-price"),
            new("PictureFileName", "2.png"), new("CatalogBrandId", "2"), new("CatalogTypeId", "1"),
            new("AvailableStock", "1"), new("RestockThreshold", "1"), new("MaxStockThreshold", "1"),
        ]), Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(Token);
        Assert.Contains("field-validation-error", html);
        Assert.Contains("<img class=\"col-md-6 esh-picture\" src=\"http://localhost/items/2/pic\" />", html);
    }

    [Fact]
    public async Task CreatePost_PathAsPictureFileName_ShowsFieldValidationErrorAndCreatesNothing()
    {
        // Decision R-9: the Create form has no picture field, but the action binds it from the POST.
        using var client = factory.CreateClient();
        var form = await client.GetStringAsync("/Catalog/Create", Token);
        var token = TokenValue().Match(form).Groups["token"].Value;
        var name = "r9-" + Guid.NewGuid().ToString("N")[..8];

        using var response = await client.PostAsync("/Catalog/Create", new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", token), new("Name", name), new("Price", "1.00"),
            new("PictureFileName", @"..\Web.config"), new("CatalogBrandId", "2"), new("CatalogTypeId", "1"),
            new("AvailableStock", "1"), new("RestockThreshold", "1"), new("MaxStockThreshold", "1"),
        ]), Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(Token);
        Assert.Matches("<span class=\"[^\"]*field-validation-error[^\"]*\" data-valmsg-for=\"PictureFileName\"[^>]*>The field Picture name must be", html);
        Assert.DoesNotContain(name, await client.GetStringAsync("/Catalog/Index?pageSize=100", Token));
    }

    private static string Footer(string html) => FooterSmall().Match(html).Groups["text"].Value.Trim();

    [GeneratedRegex("<footer.*?<small>(?<text>.*?)</small>", RegexOptions.Singleline)]
    private static partial Regex FooterSmall();

    [GeneratedRegex("<(?:link[^>]*href|script[^>]*src|img[^>]*src)=\"(?<url>/[^\"]+)\"")]
    private static partial Regex AssetUrl();

    [GeneratedRegex("url\\(\"\\.\\./(?<path>[^\"?#]+)")]
    private static partial Regex CssUrl();

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"")]
    private static partial Regex TokenValue();
}
