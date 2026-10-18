using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eShopMvc.Web.Models;

// Matches the legacy EF6 schema (docs/assessment.md 4.2). Required columns are explicit because
// the C# properties are nullable (Decision D3). Constraint/index names are EF Core defaults (D9).
public class CatalogDBContext : DbContext
{
    public const string CatalogItemHiLoSequence = "catalog_hilo";
    public const string CatalogBrandHiLoSequence = "catalog_brand_hilo";
    public const string CatalogTypeHiLoSequence = "catalog_type_hilo";

    public CatalogDBContext(DbContextOptions<CatalogDBContext> options) : base(options)
    {
    }

    public DbSet<CatalogItem> CatalogItems { get; set; }

    public DbSet<CatalogBrand> CatalogBrands { get; set; }

    public DbSet<CatalogType> CatalogTypes { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Same as the legacy SQL scripts: bigint, START WITH 1, INCREMENT BY 10 (Decision D7).
        // The brand/type sequences are created but unused, as in legacy (Decision D8).
        builder.HasSequence<long>(CatalogItemHiLoSequence).StartsAt(1).IncrementsBy(10);
        builder.HasSequence<long>(CatalogBrandHiLoSequence).StartsAt(1).IncrementsBy(10);
        builder.HasSequence<long>(CatalogTypeHiLoSequence).StartsAt(1).IncrementsBy(10);

        ConfigureCatalogType(builder.Entity<CatalogType>());
        ConfigureCatalogBrand(builder.Entity<CatalogBrand>());
        ConfigureCatalogItem(builder.Entity<CatalogItem>());
    }

    private static void ConfigureCatalogType(EntityTypeBuilder<CatalogType> builder)
    {
        builder.ToTable("CatalogType");

        builder.HasKey(ci => ci.Id);

        // IDENTITY, as EF6 created it by convention (Decision D8).
        builder.Property(ci => ci.Id)
            .UseIdentityColumn();

        builder.Property(cb => cb.Type)
            .IsRequired()
            .HasMaxLength(100);
    }

    private static void ConfigureCatalogBrand(EntityTypeBuilder<CatalogBrand> builder)
    {
        builder.ToTable("CatalogBrand");

        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.Id)
            .UseIdentityColumn();

        builder.Property(cb => cb.Brand)
            .IsRequired()
            .HasMaxLength(100);
    }

    private static void ConfigureCatalogItem(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable("Catalog");

        builder.HasKey(ci => ci.Id);

        // Not an identity column: ids come from the catalog_hilo sequence (Decision R-21).
        builder.Property(ci => ci.Id)
            .UseHiLo(CatalogItemHiLoSequence);

        builder.Property(ci => ci.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(ci => ci.Price)
            .HasPrecision(18, 2);

        builder.Property(ci => ci.PictureFileName)
            .IsRequired();

        builder.Ignore(ci => ci.PictureUri);

        builder.HasOne(ci => ci.CatalogBrand)
            .WithMany()
            .HasForeignKey(ci => ci.CatalogBrandId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ci => ci.CatalogType)
            .WithMany()
            .HasForeignKey(ci => ci.CatalogTypeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
