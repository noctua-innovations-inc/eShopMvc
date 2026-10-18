using eShopMvc.Web.Models;
using eShopMvc.Web.Models.Infrastructure;
using eShopMvc.Web.ViewModel;

namespace eShopMvc.Web.Services;

// Registered as a singleton, so the list is shared by all requests (Decision R-14):
// every access takes the lock, and every item returned has CatalogBrand/CatalogType set,
// as the EF service does with Include().
public class CatalogServiceMock : ICatalogService
{
    private readonly List<CatalogItem> catalogItems;
    private readonly List<CatalogBrand> catalogBrands;
    private readonly List<CatalogType> catalogTypes;
    private readonly Lock sync = new();

    public CatalogServiceMock()
    {
        catalogItems = new List<CatalogItem>(PreconfiguredData.GetPreconfiguredCatalogItems());
        catalogBrands = PreconfiguredData.GetPreconfiguredCatalogBrands().ToList();
        catalogTypes = PreconfiguredData.GetPreconfiguredCatalogTypes().ToList();
    }

    public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize = 10, int pageIndex = 0)
    {
        lock (sync)
        {
            var itemsOnPage = catalogItems
                .OrderBy(c => c.Id)
                .Skip(pageSize * pageIndex)
                .Take(pageSize)
                .ToList();
            itemsOnPage.ForEach(Compose);

            return new PaginatedItemsViewModel<CatalogItem>(
                pageIndex, pageSize, catalogItems.Count, itemsOnPage);
        }
    }

    public CatalogItem? FindCatalogItem(int id)
    {
        lock (sync)
        {
            // Returns the stored instance: RemoveCatalogItem matches by reference, like legacy.
            var item = catalogItems.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                Compose(item);
            }

            return item;
        }
    }

    public IEnumerable<CatalogType> GetCatalogTypes()
    {
        return PreconfiguredData.GetPreconfiguredCatalogTypes();
    }

    public IEnumerable<CatalogBrand> GetCatalogBrands()
    {
        return PreconfiguredData.GetPreconfiguredCatalogBrands();
    }

    public void CreateCatalogItem(CatalogItem catalogItem)
    {
        lock (sync)
        {
            var maxId = catalogItems.Max(i => i.Id);
            catalogItem.Id = ++maxId;
            catalogItems.Add(catalogItem);
        }
    }

    public void UpdateCatalogItem(CatalogItem modifiedItem)
    {
        lock (sync)
        {
            var index = catalogItems.FindIndex(x => x.Id == modifiedItem.Id);
            if (index >= 0)
            {
                catalogItems[index] = modifiedItem;
            }
        }
    }

    public void RemoveCatalogItem(CatalogItem catalogItem)
    {
        lock (sync)
        {
            catalogItems.Remove(catalogItem);
        }
    }

    // Caller holds the lock. Like legacy, an unknown brand/type id throws.
    private void Compose(CatalogItem item)
    {
        item.CatalogBrand = catalogBrands.First(b => b.Id == item.CatalogBrandId);
        item.CatalogType = catalogTypes.First(t => t.Id == item.CatalogTypeId);
    }
}
