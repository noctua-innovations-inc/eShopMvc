using eShopMvc.Web.Models;
using eShopMvc.Web.ViewModel;

namespace eShopMvc.Web.Services;

// Not IDisposable (Decision D2): the container owns service lifetimes.
public interface ICatalogService
{
    CatalogItem? FindCatalogItem(int id);
    IEnumerable<CatalogBrand> GetCatalogBrands();
    PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex);
    IEnumerable<CatalogType> GetCatalogTypes();
    void CreateCatalogItem(CatalogItem catalogItem);
    void UpdateCatalogItem(CatalogItem catalogItem);
    void RemoveCatalogItem(CatalogItem catalogItem);
}
