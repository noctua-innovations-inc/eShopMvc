using System.Net;
using System.Text.RegularExpressions;

namespace eShopMvc.Web.IntegrationTests.Web;

/// <summary>The style sheets the React shell and the error page link, and what they reference.</summary>
public sealed partial class StaticAssetTests(MockModeAppFactory factory) : IClassFixture<MockModeAppFactory>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/lib/bootstrap/dist/css/bootstrap.min.css")]
    [InlineData("/css/custom.css")]
    [InlineData("/css/base.css")]
    [InlineData("/css/site.css")]
    [InlineData("/images/brand.png")]
    [InlineData("/images/brand_dark.png")]
    [InlineData("/images/main_footer_text.png")]
    [InlineData("/favicon.ico")]
    public async Task StyleSheetsAndLayoutImages_Load(string path)
    {
        // Decision P2-11: linked by the client's index.html and by _Layout.cshtml (error page).
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync(path, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/lib/jquery/dist/jquery.min.js")]
    [InlineData("/lib/bootstrap/dist/js/bootstrap.min.js")]
    [InlineData("/lib/jquery-validation/dist/jquery.validate.min.js")]
    [InlineData("/lib/jquery-validation-unobtrusive/dist/jquery.validate.unobtrusive.min.js")]
    public async Task ScriptsOfTheRazorUi_AreGone(string path)
    {
        // Decision P2-11: removed with LibMan at the cutover; React needs none of them.
        using var client = factory.CreateRawClient();

        using var response = await client.GetAsync(path, Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    [GeneratedRegex("url\\(\"\\.\\./(?<path>[^\"?#]+)")]
    private static partial Regex CssUrl();
}
