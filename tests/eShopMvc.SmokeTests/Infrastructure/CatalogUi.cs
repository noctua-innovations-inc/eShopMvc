using System.Net;
using System.Text.RegularExpressions;

namespace eShopMvc.SmokeTests.Infrastructure;

/// <summary>One row of the catalog table on the Index page.</summary>
public sealed record CatalogRow(
    int Id,
    string Name,
    string Description,
    string Brand,
    string Type,
    string Price,
    string PictureFileName,
    string ThumbnailSrc);

/// <summary>Drives the catalog pages the way a user does: through the list and the HTML forms.</summary>
public static partial class CatalogUi
{
    /// <summary>Every item a smoke test creates has a name with this prefix (and only those are ever deleted).</summary>
    public const string SmokePrefix = "smoke-";

    /// <summary>Paths that render the catalog list (default route).</summary>
    public static readonly string[] IndexPaths = ["/", "/Catalog", "/Catalog/Index"];

    private const int ScanPageSize = 100;

    /// <summary>A unique item name, e.g. <c>smoke-1a2b3c4d</c> (the Name column allows 50 characters).</summary>
    public static string NewItemName() => SmokePrefix + Guid.NewGuid().ToString("N")[..8];

    public static IReadOnlyList<CatalogRow> Rows(HtmlPage page)
    {
        var rows = new List<CatalogRow>();
        foreach (var tr in page.All("table.table tbody tr"))
        {
            var cells = tr.QuerySelectorAll("td").ToList();
            if (cells.Count == 0)
            {
                continue;
            }

            var editHref = tr.QuerySelector("a[href*='/Catalog/Edit/']")?.GetAttribute("href")
                ?? throw new Xunit.Sdk.XunitException($"Catalog row without an Edit link on {page.Url}.");
            var id = int.Parse(EditLinkRegex().Match(editHref).Groups[1].Value);

            rows.Add(new CatalogRow(
                id,
                Name: Cell(cells, 1),
                Description: Cell(cells, 2),
                Brand: Cell(cells, 3),
                Type: Cell(cells, 4),
                Price: Cell(cells, 5),
                PictureFileName: Cell(cells, 6),
                ThumbnailSrc: tr.QuerySelector("img.esh-thumbnail")?.GetAttribute("src") ?? string.Empty));
        }

        return rows;
    }

    /// <summary>Reads every page of the catalog list.</summary>
    public static async Task<IReadOnlyList<CatalogRow>> AllRowsAsync(AppClient client)
    {
        var all = new List<CatalogRow>();
        for (var pageIndex = 0; pageIndex < 1000; pageIndex++)
        {
            var page = await client.GetPageAsync($"/Catalog/Index?pageSize={ScanPageSize}&pageIndex={pageIndex}");
            var rows = Rows(page);
            all.AddRange(rows);
            if (rows.Count < ScanPageSize)
            {
                break;
            }
        }

        return all;
    }

    public static async Task<CatalogRow?> FindAsync(AppClient client, Func<CatalogRow, bool> predicate) =>
        (await AllRowsAsync(client)).FirstOrDefault(predicate);

    public static Task<CatalogRow?> FindByNameAsync(AppClient client, string name) =>
        FindAsync(client, r => r.Name == name);

    /// <summary>
    /// Asserts a 302 back to the catalog list. Legacy <c>RedirectToAction("Index")</c> yields <c>/</c>
    /// because Catalog/Index is the default route; <c>/Catalog</c> and <c>/Catalog/Index</c> are equivalent.
    /// </summary>
    public static void AssertRedirectToIndex(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location ?? throw new Xunit.Sdk.XunitException("302 without a Location header.");
        var path = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString.Split('?')[0];
        Assert.Contains(path.TrimEnd('/') is "" ? "/" : path.TrimEnd('/'), IndexPaths, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Fills the Create/Edit form fields with a recognisable, valid item.</summary>
    public static void FillValidItem(Dictionary<string, string> fields, string name, string brandId, string typeId, string price = "12.34")
    {
        fields["Name"] = name;
        fields["Description"] = $"Smoke test item {name}";
        fields["Price"] = price;
        fields["CatalogBrandId"] = brandId;
        fields["CatalogTypeId"] = typeId;
        fields["AvailableStock"] = "7";
        fields["RestockThreshold"] = "2";
        fields["MaxStockThreshold"] = "20";
    }

    /// <summary>GETs the Create form and posts it after <paramref name="customize"/> has adjusted the fields.</summary>
    public static async Task<HttpResponseMessage> PostCreateAsync(AppClient client, Action<HtmlPage, Dictionary<string, string>> customize)
    {
        var form = await client.GetPageAsync("/Catalog/Create");
        var fields = form.FormFields();
        customize(form, fields);
        return await client.PostFormAsync("/Catalog/Create", fields);
    }

    /// <summary>GETs the Edit form of an item and posts it after <paramref name="customize"/> has adjusted the fields.</summary>
    public static async Task<HttpResponseMessage> PostEditAsync(AppClient client, int id, Action<HtmlPage, Dictionary<string, string>> customize)
    {
        var form = await client.GetPageAsync($"/Catalog/Edit/{id}");
        var fields = form.FormFields();
        customize(form, fields);
        return await client.PostFormAsync($"/Catalog/Edit/{id}", fields);
    }

    /// <summary>Deletes an item through the confirmation form. Returns false if it no longer exists.</summary>
    public static async Task<bool> DeleteAsync(AppClient client, int id)
    {
        using (var probe = await client.GetAsync($"/Catalog/Delete/{id}"))
        {
            if (probe.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        var form = await client.GetPageAsync($"/Catalog/Delete/{id}");
        using var response = await client.PostFormAsync($"/Catalog/Delete/{id}", form.FormFields());
        AssertRedirectToIndex(response);
        return true;
    }

    private static string Cell(List<AngleSharp.Dom.IElement> cells, int index) =>
        index < cells.Count ? HtmlPage.Normalize(cells[index].TextContent) : string.Empty;

    [GeneratedRegex(@"/Catalog/Edit/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex EditLinkRegex();
}
