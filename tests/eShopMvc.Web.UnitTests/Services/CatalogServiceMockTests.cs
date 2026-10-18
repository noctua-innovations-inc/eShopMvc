using eShopMvc.Web.Models;
using eShopMvc.Web.Services;

namespace eShopMvc.Web.UnitTests.Services;

// Each test gets a fresh mock seeded with PreconfiguredData: 12 items (ids 1-12), 5 brands, 4 types.
public class CatalogServiceMockTests
{
    private readonly CatalogServiceMock service = new();

    [Fact]
    public void GetCatalogItemsPaginated_FirstPage_ReturnsPageSizeItemsInIdOrderWithTotals()
    {
        var page = service.GetCatalogItemsPaginated(pageSize: 5, pageIndex: 0);

        Assert.Equal([1, 2, 3, 4, 5], page.Data.Select(i => i.Id));
        Assert.Equal(0, page.ActualPage);
        Assert.Equal(5, page.ItemsPerPage);
        Assert.Equal(12, page.TotalItems);
        Assert.Equal(3, page.TotalPages);
    }

    [Fact]
    public void GetCatalogItemsPaginated_LastPartialPage_ReturnsRemainingItems()
    {
        var page = service.GetCatalogItemsPaginated(pageSize: 5, pageIndex: 2);

        Assert.Equal([11, 12], page.Data.Select(i => i.Id));
    }

    [Fact]
    public void GetCatalogItemsPaginated_BeyondLastPage_IsEmptyWithUnchangedTotals()
    {
        var page = service.GetCatalogItemsPaginated(pageSize: 10, pageIndex: 5);

        Assert.Empty(page.Data);
        Assert.Equal(12, page.TotalItems);
        Assert.Equal(2, page.TotalPages);
    }

    [Fact]
    public void GetCatalogItemsPaginated_SetsBrandAndTypeOnReturnedItems()
    {
        var page = service.GetCatalogItemsPaginated(pageSize: 12, pageIndex: 0);

        Assert.All(page.Data, item =>
        {
            Assert.Equal(item.CatalogBrandId, item.CatalogBrand?.Id);
            Assert.Equal(item.CatalogTypeId, item.CatalogType?.Id);
        });
        var first = page.Data.First();
        Assert.Equal(".NET", first.CatalogBrand?.Brand);
        Assert.Equal("T-Shirt", first.CatalogType?.Type);
    }

    [Fact]
    public void FindCatalogItem_Known_ReturnsItemWithBrandAndTypeWithoutPagingFirst()
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
        Assert.Null(service.FindCatalogItem(999));
    }

    [Fact]
    public void GetCatalogBrands_ReturnsSeedBrands()
    {
        Assert.Equal(
            [(1, "Azure"), (2, ".NET"), (3, "Visual Studio"), (4, "SQL Server"), (5, "Other")],
            service.GetCatalogBrands().Select(b => (b.Id, b.Brand)));
    }

    [Fact]
    public void GetCatalogTypes_ReturnsSeedTypes()
    {
        Assert.Equal(
            [(1, "Mug"), (2, "T-Shirt"), (3, "Sheet"), (4, "USB Memory Stick")],
            service.GetCatalogTypes().Select(t => (t.Id, t.Type)));
    }

    [Fact]
    public void CreateCatalogItem_AssignsMaxIdPlusOneAndAddsItem()
    {
        var item = NewItem("created");

        service.CreateCatalogItem(item);

        Assert.Equal(13, item.Id);
        Assert.Same(item, service.FindCatalogItem(13));
        var page = service.GetCatalogItemsPaginated(pageSize: 20, pageIndex: 0);
        Assert.Equal(13, page.TotalItems);
        Assert.Equal(13, page.Data.Last().Id);
    }

    [Fact]
    public void UpdateCatalogItem_Known_ReplacesItemWithSameId()
    {
        var modified = NewItem("updated");
        modified.Id = 4;

        service.UpdateCatalogItem(modified);

        var found = service.FindCatalogItem(4);
        Assert.NotNull(found);
        Assert.Same(modified, found);
        Assert.Equal("updated", found.Name);
        Assert.Equal(12, service.GetCatalogItemsPaginated(pageSize: 20, pageIndex: 0).TotalItems);
    }

    [Fact]
    public void UpdateCatalogItem_Unknown_DoesNothing()
    {
        var modified = NewItem("ghost");
        modified.Id = 999;

        service.UpdateCatalogItem(modified);

        Assert.Null(service.FindCatalogItem(999));
        Assert.Equal(12, service.GetCatalogItemsPaginated(pageSize: 20, pageIndex: 0).TotalItems);
    }

    [Fact]
    public void RemoveCatalogItem_FoundItem_RemovesIt()
    {
        var item = service.FindCatalogItem(5)!;

        service.RemoveCatalogItem(item);

        Assert.Null(service.FindCatalogItem(5));
        Assert.Equal(11, service.GetCatalogItemsPaginated(pageSize: 20, pageIndex: 0).TotalItems);
    }

    [Fact]
    public void RemoveCatalogItem_ItemNotInList_DoesNothing()
    {
        // Legacy List.Remove semantics (R-15): matches by reference, so an equal-looking copy is a no-op.
        var copy = NewItem("copy");
        copy.Id = 5;

        service.RemoveCatalogItem(copy);

        Assert.NotNull(service.FindCatalogItem(5));
        Assert.Equal(12, service.GetCatalogItemsPaginated(pageSize: 20, pageIndex: 0).TotalItems);
    }

    private static CatalogItem NewItem(string name) => new()
    {
        Name = name,
        Price = 1.5M,
        CatalogBrandId = 1,
        CatalogTypeId = 4,
    };
}
