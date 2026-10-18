using eShopMvc.Web.Models;
using eShopMvc.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace eShopMvc.Web.UnitTests.Models;

// Pins the legacy schema (docs/assessment.md 4.2) and the step-5 note under Decision D3 by reading
// the EF Core model only; no database connection is opened.
public class CatalogDBContextModelTests
{
    private static readonly IModel Model = CreateModel();

    [Theory]
    [InlineData(typeof(CatalogItem), "Catalog")]
    [InlineData(typeof(CatalogBrand), "CatalogBrand")]
    [InlineData(typeof(CatalogType), "CatalogType")]
    public void Entity_MapsToLegacyTable(Type entity, string table)
    {
        Assert.Equal(table, Entity(entity).GetTableName());
    }

    [Theory]
    [InlineData(typeof(CatalogItem), nameof(CatalogItem.Name), false, 50)]
    [InlineData(typeof(CatalogItem), nameof(CatalogItem.Description), true, null)]
    [InlineData(typeof(CatalogItem), nameof(CatalogItem.PictureFileName), false, null)]
    [InlineData(typeof(CatalogItem), nameof(CatalogItem.CatalogBrandId), false, null)]
    [InlineData(typeof(CatalogItem), nameof(CatalogItem.CatalogTypeId), false, null)]
    [InlineData(typeof(CatalogBrand), nameof(CatalogBrand.Brand), false, 100)]
    [InlineData(typeof(CatalogType), nameof(CatalogType.Type), false, 100)]
    public void Property_HasLegacyNullabilityAndMaxLength(Type entity, string property, bool nullable, int? maxLength)
    {
        var p = Entity(entity).FindProperty(property)!;

        Assert.Equal(nullable, p.IsNullable);
        Assert.Equal(maxLength, p.GetMaxLength());
    }

    [Fact]
    public void Price_IsDecimal18_2()
    {
        Assert.Equal("decimal(18,2)", Entity(typeof(CatalogItem)).FindProperty(nameof(CatalogItem.Price))!.GetColumnType());
    }

    [Fact]
    public void PictureUri_IsNotMapped()
    {
        Assert.Null(Entity(typeof(CatalogItem)).FindProperty(nameof(CatalogItem.PictureUri)));
    }

    [Fact]
    public void CatalogItemId_UsesHiLoSequenceNotIdentity()
    {
        var id = Entity(typeof(CatalogItem)).FindProperty(nameof(CatalogItem.Id))!;

        Assert.Equal(SqlServerValueGenerationStrategy.SequenceHiLo, id.GetValueGenerationStrategy());
        Assert.Equal(CatalogDBContext.CatalogItemHiLoSequence, id.GetHiLoSequenceName());
    }

    [Theory]
    [InlineData(typeof(CatalogBrand))]
    [InlineData(typeof(CatalogType))]
    public void BrandAndTypeIds_AreIdentityColumns(Type entity)
    {
        var id = Entity(entity).FindProperty("Id")!;

        Assert.Equal(SqlServerValueGenerationStrategy.IdentityColumn, id.GetValueGenerationStrategy());
    }

    [Theory]
    [InlineData(CatalogDBContext.CatalogItemHiLoSequence)]
    [InlineData(CatalogDBContext.CatalogBrandHiLoSequence)]
    [InlineData(CatalogDBContext.CatalogTypeHiLoSequence)]
    public void Sequence_MatchesLegacyScripts(string name)
    {
        var sequence = Model.FindSequence(name)!;

        Assert.Equal(typeof(long), sequence.Type);
        Assert.Equal(1, sequence.StartValue);
        Assert.Equal(10, sequence.IncrementBy);
    }

    [Theory]
    [InlineData(nameof(CatalogItem.CatalogBrandId), typeof(CatalogBrand))]
    [InlineData(nameof(CatalogItem.CatalogTypeId), typeof(CatalogType))]
    public void ForeignKey_IsRequiredAndCascades(string property, Type principal)
    {
        var fk = Assert.Single(Entity(typeof(CatalogItem)).GetForeignKeys(), f => f.Properties.Single().Name == property);

        Assert.Equal(principal, fk.PrincipalEntityType.ClrType);
        Assert.True(fk.IsRequired);
        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
    }

    private static IEntityType Entity(Type type) => Model.FindEntityType(type)!;

    private static IModel CreateModel()
    {
        var builder = new DbContextOptionsBuilder<CatalogDBContext>();
        builder.UseCatalogDatabase("Server=unused;Database=unused");
        using var context = new CatalogDBContext(builder.Options);
        return context.GetService<IDesignTimeModel>().Model;
    }
}
