using eShopMvc.Web.Models;

namespace eShopMvc.Web.Services;

/// <summary>
/// Decision P2-6 (overposting): the fields a client may set on a catalog item, the same as the Razor
/// forms' <c>[Bind]</c> list in <c>CatalogController</c> except <c>Id</c>, which comes from the URL or
/// the service. <c>PictureUri</c> and the navigation properties are never taken from the client.
/// </summary>
public static class CatalogItemFields
{
    public static void CopyEditable(CatalogItem source, CatalogItem target)
    {
        target.Name = source.Name;
        target.Description = source.Description;
        target.Price = source.Price;
        target.PictureFileName = source.PictureFileName;
        target.CatalogTypeId = source.CatalogTypeId;
        target.CatalogBrandId = source.CatalogBrandId;
        target.AvailableStock = source.AvailableStock;
        target.RestockThreshold = source.RestockThreshold;
        target.MaxStockThreshold = source.MaxStockThreshold;
        target.OnReorder = source.OnReorder;
    }
}
