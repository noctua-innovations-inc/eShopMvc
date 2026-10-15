using System.Net;
using System.Text.RegularExpressions;
using eShopMvc.SmokeTests.Infrastructure;

namespace eShopMvc.SmokeTests.Catalog;

/// <summary>Catalog list (default route), paging, layout footer and referenced static assets.</summary>
[Trait("Category", "Catalog")]
public sealed partial class IndexAndPagingTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/Catalog")]
    [InlineData("/Catalog/Index")]
    public async Task Index_RendersCatalogTablePagerAndCreateLink(string path)
    {
        using var client = new AppClient();

        var page = await client.GetPageAsync(path);

        page.Single("table.table");
        page.Single(".esh-pager");
        var create = page.Single("a[href='/Catalog/Create']");
        Assert.Equal("Create New", create.TextContent.Trim());
    }

    [Fact]
    public async Task Index_EachRowLinksToItsActionsAndPicture()
    {
        using var client = new AppClient();

        var page = await client.GetPageAsync("/Catalog/Index?pageSize=5&pageIndex=0");
        var rows = page.All("table.table tbody tr").Where(tr => tr.QuerySelector("td") is not null).ToList();

        Assert.NotEmpty(rows);
        foreach (var row in CatalogUi.Rows(page))
        {
            var tr = rows.Single(r => r.QuerySelector($"a[href$='/Catalog/Edit/{row.Id}']") is not null);
            foreach (var action in new[] { "Edit", "Details", "Delete" })
            {
                var link = tr.QuerySelector($"a[href$='/Catalog/{action}/{row.Id}']");
                Assert.True(link is not null, $"Row {row.Id} has no {action} link.");
                Assert.Equal(action, link!.TextContent.Trim());
            }

            Assert.Equal($"/items/{row.Id}/pic", new Uri(SmokeSettings.BaseUrl, row.ThumbnailSrc).AbsolutePath);
        }
    }

    [Fact]
    public async Task Paging_FirstPageOfSizeOne_ShowsOneRowAndPagerText()
    {
        using var client = new AppClient();

        var page = await client.GetPageAsync("/Catalog/Index?pageSize=1&pageIndex=0");

        Assert.Single(CatalogUi.Rows(page));
        var (pageNumber, totalPages, totalItems) = ReadPager(page, expectedPageSize: 1);
        Assert.Equal(1, pageNumber);
        Assert.True(totalItems >= 2, "Paging tests need at least two catalog items (the seed has 12).");
        Assert.Equal(totalItems, totalPages);

        var next = NavLink(page, "Next");
        var href = WebUtility.HtmlDecode(next.GetAttribute("href")!);
        Assert.Contains("pageIndex=1", href);
        Assert.Contains("pageSize=1", href);
        Assert.DoesNotContain("esh-pager-item--hidden", next.ClassName ?? string.Empty);
    }

    [Fact]
    public async Task Paging_SecondPage_ShowsNextItemInIdOrder()
    {
        using var client = new AppClient();

        var first = CatalogUi.Rows(await client.GetPageAsync("/Catalog/Index?pageSize=1&pageIndex=0")).Single();
        var secondPage = await client.GetPageAsync("/Catalog/Index?pageSize=1&pageIndex=1");
        var second = CatalogUi.Rows(secondPage).Single();

        Assert.Equal(2, ReadPager(secondPage, expectedPageSize: 1).PageNumber);
        Assert.True(second.Id > first.Id, $"Items are ordered by Id: page 2 has {second.Id}, page 1 has {first.Id}.");
    }

    [Fact]
    public async Task Paging_BeyondLastPage_ShowsNoRows()
    {
        using var client = new AppClient();

        var page = await client.GetPageAsync("/Catalog/Index?pageSize=10&pageIndex=100000");

        page.Single("table.table");
        Assert.Empty(CatalogUi.Rows(page));
    }

    [Fact]
    public async Task Layout_FooterShowsSessionInfo()
    {
        using var client = new AppClient();

        var page = await client.GetPageAsync("/");

        // Legacy renders "{MachineName}, {SessionStartTime}" (Session_Start → _Layout footer).
        var text = page.Single("footer small").TextContent.Trim();
        Assert.Matches(@"^\S.*, \S.*$", text);
    }

    [Fact]
    public async Task Layout_AllReferencedStylesheetsScriptsAndImagesLoad()
    {
        using var client = new AppClient();
        var page = await client.GetPageAsync("/");

        var urls = page.All("link[rel='stylesheet'][href]").Select(e => e.GetAttribute("href")!)
            .Concat(page.All("script[src]").Select(e => e.GetAttribute("src")!))
            .Concat(page.All("img[src]").Select(e => e.GetAttribute("src")!))
            .Select(u => new Uri(SmokeSettings.BaseUrl, u))
            .Distinct()
            .ToList();

        Assert.NotEmpty(urls);
        var failures = new List<string>();
        foreach (var url in urls)
        {
            using var response = await client.GetAsync(url.ToString());
            if (response.StatusCode != HttpStatusCode.OK)
            {
                failures.Add($"{(int)response.StatusCode} {url.PathAndQuery}");
            }
        }

        Assert.True(failures.Count == 0, "Assets that did not load: " + string.Join(", ", failures));
    }

    private static (int PageNumber, int TotalPages, int TotalItems) ReadPager(HtmlPage page, int expectedPageSize)
    {
        var text = HtmlPage.Normalize(page.Single(".esh-pager span.esh-pager-item").TextContent);
        var match = PagerRegex().Match(text);
        Assert.True(match.Success, $"Unexpected pager text: '{text}'.");
        Assert.Equal(expectedPageSize, int.Parse(match.Groups["size"].Value));
        return (int.Parse(match.Groups["page"].Value), int.Parse(match.Groups["pages"].Value), int.Parse(match.Groups["total"].Value));
    }

    private static AngleSharp.Dom.IElement NavLink(HtmlPage page, string text) =>
        page.All(".esh-pager a").SingleOrDefault(a => a.TextContent.Trim() == text)
        ?? throw new Xunit.Sdk.XunitException($"No '{text}' link in the pager.");

    // "Showing 1 of 12 products - Page 1 - 12"
    [GeneratedRegex(@"Showing (?<size>\d+) of (?<total>\d+) products - Page (?<page>\d+) - (?<pages>\d+)")]
    private static partial Regex PagerRegex();
}
