using eShopMvc.Web.Models;
using eShopMvc.Web.Models.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace eShopMvc.Web.Services;

public static class CatalogServiceCollectionExtensions
{
    /// <summary>Legacy connection string name (Web.config); value per environment (Decision D5).</summary>
    public const string ConnectionStringName = "CatalogDBContext";

    /// <summary>
    /// Binds <see cref="CatalogOptions"/> and registers the <see cref="ICatalogService"/> implementation
    /// for the configured mode (legacy: Modules/ApplicationModule.cs).
    /// </summary>
    public static IServiceCollection AddCatalogServices(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(CatalogOptions.SectionName);
        services.Configure<CatalogOptions>(section);

        var options = section.Get<CatalogOptions>() ?? new CatalogOptions();
        if (options.UseMockData)
        {
            services.AddSingleton<ICatalogService, CatalogServiceMock>();
            return services;
        }

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{CatalogOptions.SectionName}:{nameof(CatalogOptions.UseMockData)} is false (database mode), but " +
                $"ConnectionStrings:{ConnectionStringName} is not set. Set it (e.g. environment variable " +
                $"ConnectionStrings__{ConnectionStringName}), or set {CatalogOptions.SectionName}:{nameof(CatalogOptions.UseMockData)}=true.");
        }

        services.AddDbContext<CatalogDBContext>(db => db.UseCatalogDatabase(connectionString));
        services.AddScoped<ICatalogService, CatalogService>();
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
    /// </summary>
    public static async Task InitializeCatalogDatabaseAsync(this IHost app)
    {
        if (app.Services.GetRequiredService<IOptions<CatalogOptions>>().Value.UseMockData)
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatalogDBContext>().Database.MigrateAsync();
    }
}
