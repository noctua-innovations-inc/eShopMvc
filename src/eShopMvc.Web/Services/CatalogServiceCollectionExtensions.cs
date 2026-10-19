using eShopMvc.Web.Models;
using eShopMvc.Web.Models.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace eShopMvc.Web.Services;

public static class CatalogServiceCollectionExtensions
{
    /// <summary>Legacy connection string name (Web.config); value per environment (Decision D5).</summary>
    public const string ConnectionStringName = "CatalogDBContext";

    /// <summary>
    /// Validates the catalog configuration (<see cref="CatalogSettings"/>) and registers the
    /// <see cref="ICatalogService"/> implementation for the configured mode (legacy: Modules/ApplicationModule.cs).
    /// </summary>
    public static IServiceCollection AddCatalogServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Throws before the app is built, listing every problem (step 8).
        var options = CatalogSettings.Validate(configuration);
        services.AddOptions<CatalogOptions>().Configure(o => o.UseMockData = options.UseMockData);

        if (options.UseMockData)
        {
            services.AddSingleton<ICatalogService, CatalogServiceMock>();
            return services;
        }

        var connectionString = configuration.GetConnectionString(ConnectionStringName)!;
        services.AddDbContext<CatalogDBContext>(db => db.UseCatalogDatabase(connectionString));
        services.AddScoped<ICatalogService, CatalogService>();

        // Decision L3: readiness includes the database in database mode.
        services.AddHealthChecks().AddCheck<CatalogDatabaseHealthCheck>(
            CatalogDatabaseHealthCheck.Name, tags: [HealthEndpoints.ReadyTag], timeout: TimeSpan.FromSeconds(5));
        return services;
    }

    /// <summary>SQL Server plus seeding for both Migrate() and MigrateAsync(). Also used by the integration tests.</summary>
    public static DbContextOptionsBuilder UseCatalogDatabase(this DbContextOptionsBuilder builder, string connectionString)
    {
        return builder
            .UseSqlServer(connectionString)
            .UseSeeding((context, _) => CatalogDBInitializer.Seed(context))
            .UseAsyncSeeding((context, _, cancellationToken) => CatalogDBInitializer.SeedAsync(context, cancellationToken));
    }

    /// <summary>
    /// In database mode, creates or migrates the database and seeds it if empty
    /// (legacy: CreateDatabaseIfNotExists + Seed, run on first request). No-op with mock data.
    /// A failure is logged as Critical with the server, database and setting to check, then rethrown.
    /// </summary>
    public static async Task InitializeCatalogDatabaseAsync(this IHost app)
    {
        if (app.Services.GetRequiredService<IOptions<CatalogOptions>>().Value.UseMockData)
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDBContext>();
        try
        {
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            // Server and database only: the connection string may contain a password.
            var target = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
            app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(CatalogDBContext).FullName!).LogCritical(ex,
                "Could not create or migrate the catalog database {Database} on server {Server}. Check that the server is " +
                "running and reachable, and check ConnectionStrings:{ConnectionStringName} (environment variable " +
                "ConnectionStrings__{ConnectionStringName}).",
                target.InitialCatalog, target.DataSource, ConnectionStringName, ConnectionStringName);
            throw;
        }
    }
}
