namespace eShopMvc.SmokeTests.Infrastructure;

/// <summary>
/// A catalog item owned by one test: created through the Create form, deleted through the Delete form
/// when disposed. Tests that change data work only on their own item, never on seeded ones.
/// </summary>
public sealed class CatalogItemScope : IAsyncDisposable
{
    private CatalogItemScope(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; }

    public string Name { get; }

    public string Description { get; private init; } = string.Empty;

    public string Price { get; private init; } = string.Empty;

    public string BrandText { get; private init; } = string.Empty;

    public string TypeText { get; private init; } = string.Empty;

    /// <summary>Creates a valid item (first brand and type in the form, price 12.34) and finds its id in the list.</summary>
    public static async Task<CatalogItemScope> CreateAsync(AppClient client)
    {
        var name = CatalogUi.NewItemName();
        (string Value, string Text) brand = (string.Empty, string.Empty), type = (string.Empty, string.Empty);

        using (var response = await CatalogUi.PostCreateAsync(client, (form, fields) =>
               {
                   brand = form.SelectOptions("CatalogBrandId")[0];
                   type = form.SelectOptions("CatalogTypeId")[0];
                   CatalogUi.FillValidItem(fields, name, brand.Value, type.Value);
               }))
        {
            CatalogUi.AssertRedirectToIndex(response);
        }

        var row = await CatalogUi.FindByNameAsync(client, name)
            ?? throw new Xunit.Sdk.XunitException($"Created item '{name}' was not found in the catalog list.");

        return new CatalogItemScope(row.Id, name)
        {
            Description = row.Description,
            Price = "12.34",
            BrandText = brand.Text,
            TypeText = type.Text,
        };
    }

    /// <summary>Takes ownership of an item created some other way, so it is cleaned up.</summary>
    public static CatalogItemScope Adopt(int id, string name) => new(id, name);

    public async ValueTask DisposeAsync()
    {
        try
        {
            using var client = new AppClient();
            await CatalogUi.DeleteAsync(client, Id);
        }
        catch (Exception ex)
        {
            // Don't mask the test's own failure; the next run's sweep removes leftover smoke-* items.
            TestContext.Current.AddWarning($"Cleanup of catalog item {Id} ('{Name}') failed: {ex.Message}");
        }
    }
}
