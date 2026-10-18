using eShopMvc.Web.Models;
using eShopMvc.Web.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

[assembly: AssemblyFixture(typeof(eShopMvc.Web.IntegrationTests.Infrastructure.TestDatabase))]

namespace eShopMvc.Web.IntegrationTests.Infrastructure;

/// <summary>
/// A dedicated test database, never the app's own: <c>ESHOP_TEST_DB</c> (a full connection string),
/// default LocalDB <c>eShopMvc.CatalogDb.Tests</c>. Recreated once per run with MigrateAsync (so it's
/// seeded by UseAsyncSeeding) and dropped at the end. Tests run serially (xunit.runner.json).
/// </summary>
public sealed class TestDatabase : IAsyncLifetime
{
    public const string ConnectionStringVariable = "ESHOP_TEST_DB";

    public static string ConnectionString { get; } = ReadConnectionString();

    public async ValueTask InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    public static CatalogDBContext CreateContext(string? connectionString = null)
    {
        var builder = new DbContextOptionsBuilder<CatalogDBContext>();
        builder.UseCatalogDatabase(connectionString ?? ConnectionString);
        return new CatalogDBContext(builder.Options);
    }

    /// <summary>The same server with a different database name, for tests that need a database of their own.</summary>
    public static string WithDatabaseSuffix(string suffix)
    {
        var builder = new SqlConnectionStringBuilder(ConnectionString);
        builder.InitialCatalog += suffix;
        return builder.ConnectionString;
    }

    private static string ReadConnectionString()
    {
        var value = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        return string.IsNullOrWhiteSpace(value)
            ? @"Server=(localdb)\MSSQLLocalDB;Database=eShopMvc.CatalogDb.Tests;Integrated Security=True"
            : value;
    }
}
