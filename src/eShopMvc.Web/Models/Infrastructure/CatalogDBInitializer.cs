using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace eShopMvc.Web.Models.Infrastructure;

/// <summary>
/// Seeds an empty catalog database from <see cref="PreconfiguredData"/> (legacy: CatalogDBInitializer.Seed).
/// Registered with both UseSeeding and UseAsyncSeeding, so Migrate() and MigrateAsync() seed the same way.
/// </summary>
/// <remarks>
/// Types, then brands, then items, as in legacy. Brands and types keep their PreconfiguredData ids
/// (IDENTITY columns, inserted under IDENTITY_INSERT; Decision D8), so the items' foreign keys always
/// match (R-19). Items get their ids from the catalog_hilo sequence (Decision R-21), which advances it
/// exactly as legacy seeding did. UseCustomizationData (CSV/ZIP seeding) is not ported (Decision D10).
/// </remarks>
public static class CatalogDBInitializer
{
    public static void Seed(DbContext context)
    {
        if (context.Set<CatalogBrand>().Any())
        {
            return;
        }

        var ownTransaction = context.Database.CurrentTransaction is null ? context.Database.BeginTransaction() : null;
        try
        {
            InsertWithExplicitIds(context, PreconfiguredData.GetPreconfiguredCatalogTypes());
            InsertWithExplicitIds(context, PreconfiguredData.GetPreconfiguredCatalogBrands());

            context.Set<CatalogItem>().AddRange(ItemsWithoutIds());
            context.SaveChanges();

            ownTransaction?.Commit();
        }
        finally
        {
            ownTransaction?.Dispose();
        }
    }

    public static async Task SeedAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Set<CatalogBrand>().AnyAsync(cancellationToken))
        {
            return;
        }

        var ownTransaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            await InsertWithExplicitIdsAsync(context, PreconfiguredData.GetPreconfiguredCatalogTypes(), cancellationToken);
            await InsertWithExplicitIdsAsync(context, PreconfiguredData.GetPreconfiguredCatalogBrands(), cancellationToken);

            // AddRangeAsync: the HiLo generator may query the sequence while adding.
            await context.Set<CatalogItem>().AddRangeAsync(ItemsWithoutIds(), cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            if (ownTransaction is not null)
            {
                await ownTransaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            if (ownTransaction is not null)
            {
                await ownTransaction.DisposeAsync();
            }
        }
    }

    // Id = 0 lets the HiLo generator assign them (1-12 on a fresh database), as legacy overwrote them.
    private static List<CatalogItem> ItemsWithoutIds()
    {
        var items = PreconfiguredData.GetPreconfiguredCatalogItems();
        items.ForEach(i => i.Id = 0);
        return items;
    }

    // IDENTITY_INSERT is per session and per table, so it's switched on around one SaveChanges
    // inside the open transaction (which keeps the connection open).
    private static void InsertWithExplicitIds<TEntity>(DbContext context, IEnumerable<TEntity> entities)
        where TEntity : class
    {
        var (on, off) = IdentityInsertSql<TEntity>(context);
        context.Set<TEntity>().AddRange(entities);
        context.Database.ExecuteSqlRaw(on);
        context.SaveChanges();
        context.Database.ExecuteSqlRaw(off);
    }

    private static async Task InsertWithExplicitIdsAsync<TEntity>(DbContext context, IEnumerable<TEntity> entities, CancellationToken cancellationToken)
        where TEntity : class
    {
        var (on, off) = IdentityInsertSql<TEntity>(context);
        context.Set<TEntity>().AddRange(entities);
        await context.Database.ExecuteSqlRawAsync(on, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(off, cancellationToken);
    }

    // The table name comes from the model, never from input, so building raw SQL with it is safe.
    private static (string On, string Off) IdentityInsertSql<TEntity>(DbContext context)
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity))!;
        var table = context.GetService<ISqlGenerationHelper>().DelimitIdentifier(entityType.GetTableName()!, entityType.GetSchema());
        return ($"SET IDENTITY_INSERT {table} ON", $"SET IDENTITY_INSERT {table} OFF");
    }
}
