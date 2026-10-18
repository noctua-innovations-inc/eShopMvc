using eShopMvc.Web.Models;
using eShopMvc.Web.ViewModel;
using Microsoft.EntityFrameworkCore;

namespace eShopMvc.Web.Services;

// Database version (legacy: Services/CatalogService.cs). Registered per request (scoped).
public class CatalogService : ICatalogService
{
    private readonly CatalogDBContext db;

    public CatalogService(CatalogDBContext db)
    {
        this.db = db;
    }

    public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex)
    {
        var totalItems = db.CatalogItems.LongCount();

        var itemsOnPage = db.CatalogItems
            .Include(c => c.CatalogBrand)
            .Include(c => c.CatalogType)
            .OrderBy(c => c.Id)
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToList();

        return new PaginatedItemsViewModel<CatalogItem>(
            pageIndex, pageSize, totalItems, itemsOnPage);
    }

    public CatalogItem? FindCatalogItem(int id)
    {
        return db.CatalogItems.Include(c => c.CatalogBrand).Include(c => c.CatalogType).FirstOrDefault(ci => ci.Id == id);
    }

    public IEnumerable<CatalogType> GetCatalogTypes()
    {
        return db.CatalogTypes;
    }

    public IEnumerable<CatalogBrand> GetCatalogBrands()
    {
        return db.CatalogBrands;
    }

    public void CreateCatalogItem(CatalogItem catalogItem)
    {
        // Legacy always overwrote Id (it's bound from the POST); HiLo only generates a value for 0.
        catalogItem.Id = 0;
        db.CatalogItems.Add(catalogItem);
        db.SaveChanges();
    }

    public void UpdateCatalogItem(CatalogItem catalogItem)
    {
        db.Entry(catalogItem).State = EntityState.Modified;
        db.SaveChanges();
    }

    public void RemoveCatalogItem(CatalogItem catalogItem)
    {
        db.CatalogItems.Remove(catalogItem);
        db.SaveChanges();
    }
}
