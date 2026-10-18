namespace eShopMvc.Web.Services;

public static class CatalogServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="CatalogOptions"/> and registers the <see cref="ICatalogService"/> implementation
    /// for the configured mode (legacy: Modules/ApplicationModule.cs).
    /// </summary>
    public static IServiceCollection AddCatalogServices(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(CatalogOptions.SectionName);
        services.Configure<CatalogOptions>(section);

        var options = section.Get<CatalogOptions>() ?? new CatalogOptions();
        if (!options.UseMockData)
        {
            throw new InvalidOperationException(
                $"{CatalogOptions.SectionName}:{nameof(CatalogOptions.UseMockData)} is false (database mode), but database mode " +
                "is not implemented yet; it arrives in step 5 (EF Core). " +
                $"Set {CatalogOptions.SectionName}:{nameof(CatalogOptions.UseMockData)}=true to run with mock data.");
        }

        services.AddSingleton<ICatalogService, CatalogServiceMock>();
        return services;
    }
}
