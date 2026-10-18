using eShopMvc.Web.IntegrationTests.Infrastructure;
using eShopMvc.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace eShopMvc.Web.IntegrationTests.Models;

public class CatalogDBInitializerTests
{
    [Fact]
    public async Task MigrateAsync_SeedsViaUseAsyncSeeding()
    {
        // The shared test database was created with MigrateAsync by the assembly fixture.
        await using var context = TestDatabase.CreateContext();

        await AssertSeededAsync(context, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Migrate_SeedsViaUseSeeding()
    {
        var connectionString = TestDatabase.WithDatabaseSuffix(".Sync");
        await using var context = TestDatabase.CreateContext(connectionString);
        context.Database.EnsureDeleted();
        try
        {
            context.Database.Migrate();

            await AssertSeededAsync(context, TestContext.Current.CancellationToken);
        }
        finally
        {
            context.Database.EnsureDeleted();
        }
    }

    [Fact]
    public async Task MigrateAsync_OnSeededDatabase_DoesNotSeedAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = TestDatabase.CreateContext();

        await context.Database.MigrateAsync(cancellationToken);

        Assert.Equal(12, await context.CatalogItems.CountAsync(cancellationToken));
        Assert.Equal(5, await context.CatalogBrands.CountAsync(cancellationToken));
        Assert.Equal(4, await context.CatalogTypes.CountAsync(cancellationToken));
    }

    private static async Task AssertSeededAsync(CatalogDBContext context, CancellationToken cancellationToken)
    {
        Assert.Equal(
            [(1, "Azure"), (2, ".NET"), (3, "Visual Studio"), (4, "SQL Server"), (5, "Other")],
            (await context.CatalogBrands.OrderBy(b => b.Id).ToListAsync(cancellationToken)).Select(b => (b.Id, b.Brand)));
        Assert.Equal(
            [(1, "Mug"), (2, "T-Shirt"), (3, "Sheet"), (4, "USB Memory Stick")],
            (await context.CatalogTypes.OrderBy(t => t.Id).ToListAsync(cancellationToken)).Select(t => (t.Id, t.Type)));

        // Ids 1-12 from catalog_hilo, in PreconfiguredData order, with the seeded foreign keys intact.
        var items = await context.CatalogItems.AsNoTracking()
            .Include(i => i.CatalogBrand).Include(i => i.CatalogType)
            .OrderBy(i => i.Id).ToListAsync(cancellationToken);
        Assert.Equal(Enumerable.Range(1, 12), items.Select(i => i.Id));
        Assert.Equal((".NET Bot Black Hoodie", ".NET", "T-Shirt", 19.5M, "1.png"),
            (items[0].Name, items[0].CatalogBrand?.Brand, items[0].CatalogType?.Type, items[0].Price, items[0].PictureFileName));
        Assert.Equal(("Prism White TShirt", "Other", "T-Shirt"),
            (items[11].Name, items[11].CatalogBrand?.Brand, items[11].CatalogType?.Type));
    }
}
