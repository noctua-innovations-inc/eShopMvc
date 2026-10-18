using eShopMvc.Web.IntegrationTests.Infrastructure;
using eShopMvc.Web.Models;
using eShopMvc.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace eShopMvc.Web.IntegrationTests.Services;

// Each test runs in a transaction that is rolled back on dispose, so the seeded data
// (12 items, 5 brands, 4 types) is the same for every test.
public sealed class CatalogServiceTests : IDisposable
{
    private readonly CatalogDBContext context;
    private readonly IDbContextTransaction transaction;
    private readonly CatalogService service;

    public CatalogServiceTests()
    {
        context = TestDatabase.CreateContext();
        transaction = context.Database.BeginTransaction();
        service = new CatalogService(context);
    }

    public void Dispose()
    {
        transaction.Dispose();
        context.Dispose();
    }

    [Fact]
    public void GetCatalogItemsPaginated_ReturnsPageInIdOrderWithBrandTypeAndTotals()
    {
        var page = service.GetCatalogItemsPaginated(pageSize: 5, pageIndex: 1);

        Assert.Equal([6, 7, 8, 9, 10], page.Data.Select(i => i.Id));
        Assert.All(page.Data, i =>
        {
            Assert.Equal(i.CatalogBrandId, i.CatalogBrand?.Id);
            Assert.Equal(i.CatalogTypeId, i.CatalogType?.Id);
        });
        Assert.Equal(12, page.TotalItems);
        Assert.Equal(3, page.TotalPages);
    }

    [Fact]
    public void GetCatalogItemsPaginated_BeyondLastPage_IsEmptyWithTotals()
    {
        var page = service.GetCatalogItemsPaginated(pageSize: 10, pageIndex: 5);

        Assert.Empty(page.Data);
        Assert.Equal(12, page.TotalItems);
    }

    [Fact]
    public void FindCatalogItem_Known_ReturnsItemWithBrandAndType()
    {
        var item = service.FindCatalogItem(3);

        Assert.NotNull(item);
        Assert.Equal("Prism White T-Shirt", item.Name);
        Assert.Equal("Other", item.CatalogBrand?.Brand);
        Assert.Equal("T-Shirt", item.CatalogType?.Type);
    }

    [Fact]
    public void FindCatalogItem_Unknown_ReturnsNull()
    {
        Assert.Null(service.FindCatalogItem(999_999));
    }

    [Fact]
    public void GetCatalogBrandsAndTypes_ReturnSeedRows()
    {
        Assert.Equal([1, 2, 3, 4, 5], service.GetCatalogBrands().Select(b => b.Id).Order());
        Assert.Equal([1, 2, 3, 4], service.GetCatalogTypes().Select(t => t.Id).Order());
    }

    [Fact]
    public void CreateCatalogItem_IgnoresSuppliedIdAndUsesHiLoId()
    {
        var item = NewItem("created");
        item.Id = 5; // bound from the POST in legacy; must not be used

        service.CreateCatalogItem(item);

        Assert.True(item.Id > 12, $"Expected a new HiLo id above the seeded ids, got {item.Id}.");
        var stored = Reload(item.Id);
        Assert.NotNull(stored);
        Assert.Equal("created", stored.Name);
        Assert.Equal("Roslyn Red Sheet", Reload(5)?.Name); // the seeded item 5 is untouched
        Assert.Equal(13, context.CatalogItems.LongCount());
    }

    [Fact]
    public void UpdateCatalogItem_PersistsAllFields()
    {
        var modified = NewItem("updated");
        modified.Id = 4;
        modified.Price = 99.99M;

        service.UpdateCatalogItem(modified);

        var stored = Reload(4);
        Assert.NotNull(stored);
        Assert.Equal(("updated", 99.99M, 1, 4), (stored.Name, stored.Price, stored.CatalogBrandId, stored.CatalogTypeId));
    }

    [Fact]
    public void RemoveCatalogItem_DeletesTheRow()
    {
        var item = service.FindCatalogItem(5)!;

        service.RemoveCatalogItem(item);

        Assert.Null(Reload(5));
        Assert.Equal(11, context.CatalogItems.LongCount());
    }

    // Reads from the database (inside the open transaction), not from the change tracker.
    private CatalogItem? Reload(int id)
    {
        context.ChangeTracker.Clear();
        return context.CatalogItems.AsNoTracking().FirstOrDefault(i => i.Id == id);
    }

    private static CatalogItem NewItem(string name) => new()
    {
        Name = name,
        Description = name,
        Price = 1.5M,
        PictureFileName = "1.png",
        CatalogBrandId = 1,
        CatalogTypeId = 4,
    };
}
