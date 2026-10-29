using eShopMvc.Web.Models;
using eShopMvc.Web.Services;

namespace eShopMvc.Web.UnitTests.Services;

// Decision P2-6 (overposting): only the Razor forms' [Bind] fields, without Id, come from the client.
public class CatalogItemFieldsTests
{
    private static readonly string[] NotEditable =
        [nameof(CatalogItem.Id), nameof(CatalogItem.PictureUri), nameof(CatalogItem.CatalogBrand), nameof(CatalogItem.CatalogType)];

    [Fact]
    public void CopyEditable_CopiesEveryEditableField()
    {
        var source = Source();
        var target = new CatalogItem();

        CatalogItemFields.CopyEditable(source, target);

        Assert.Equal("Copied", target.Name);
        Assert.Equal("Copied description", target.Description);
        Assert.Equal(98.76m, target.Price);
        Assert.Equal("copied.png", target.PictureFileName);
        Assert.Equal(3, target.CatalogTypeId);
        Assert.Equal(4, target.CatalogBrandId);
        Assert.Equal(5, target.AvailableStock);
        Assert.Equal(6, target.RestockThreshold);
        Assert.Equal(7, target.MaxStockThreshold);
        Assert.True(target.OnReorder);
    }

    [Fact]
    public void CopyEditable_IgnoresIdPictureUriAndNavigationProperties()
    {
        var brand = new CatalogBrand { Id = 1, Brand = "Kept" };
        var type = new CatalogType { Id = 1, Type = "Kept" };
        var target = new CatalogItem { Id = 42, PictureUri = "http://kept/", CatalogBrand = brand, CatalogType = type };

        CatalogItemFields.CopyEditable(Source(), target);

        Assert.Equal(42, target.Id);
        Assert.Equal("http://kept/", target.PictureUri);
        Assert.Same(brand, target.CatalogBrand);
        Assert.Same(type, target.CatalogType);
    }

    [Fact]
    public void CopyEditable_CoversEveryOtherSettableProperty()
    {
        // Fails when a property is added to CatalogItem without deciding whether clients may set it.
        var source = Source();
        var target = new CatalogItem();

        CatalogItemFields.CopyEditable(source, target);

        var notCopied = typeof(CatalogItem).GetProperties()
            .Where(p => p.CanWrite && !NotEditable.Contains(p.Name))
            .Where(p => !Equals(p.GetValue(source), p.GetValue(target)))
            .Select(p => p.Name);
        Assert.Empty(notCopied);
    }

    // Every editable field differs from a new CatalogItem's default.
    private static CatalogItem Source() => new()
    {
        Id = 99,
        Name = "Copied",
        Description = "Copied description",
        Price = 98.76m,
        PictureFileName = "copied.png",
        PictureUri = "http://ignored/",
        CatalogTypeId = 3,
        CatalogBrandId = 4,
        AvailableStock = 5,
        RestockThreshold = 6,
        MaxStockThreshold = 7,
        OnReorder = true,
        CatalogBrand = new CatalogBrand { Id = 4, Brand = "Ignored" },
        CatalogType = new CatalogType { Id = 3, Type = "Ignored" },
    };
}
