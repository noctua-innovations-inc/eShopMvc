using eShopMvc.Web.Models;
using eShopMvc.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eShopMvc.Web.UnitTests.Services;

public class CatalogServiceCollectionExtensionsTests
{
    private const string ConnectionStringKey = "ConnectionStrings:CatalogDBContext";

    [Fact]
    public void AddCatalogServices_UseMockDataTrue_RegistersMockAsSingletonAndNoDbContext()
    {
        var services = new ServiceCollection();

        services.AddCatalogServices(Configuration(("Catalog:UseMockData", "true")));

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ICatalogService));
        Assert.Equal(typeof(CatalogServiceMock), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(CatalogDBContext));
    }

    [Theory]
    [InlineData("false")]
    [InlineData(null)]
    public void AddCatalogServices_UseMockDataFalseOrMissing_RegistersDatabaseServiceAndDbContextAsScoped(string? value)
    {
        var settings = new List<(string, string)> { (ConnectionStringKey, "Server=unused;Database=unused") };
        if (value is not null)
        {
            settings.Add(("Catalog:UseMockData", value));
        }

        var services = new ServiceCollection();
        services.AddCatalogServices(Configuration([.. settings]));

        var service = Assert.Single(services, d => d.ServiceType == typeof(ICatalogService));
        Assert.Equal(typeof(CatalogService), service.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, service.Lifetime);
        var context = Assert.Single(services, d => d.ServiceType == typeof(CatalogDBContext));
        Assert.Equal(ServiceLifetime.Scoped, context.Lifetime);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddCatalogServices_DatabaseModeWithoutConnectionString_ThrowsNamingTheSetting(string? connectionString)
    {
        var configuration = connectionString is null
            ? Configuration(("Catalog:UseMockData", "false"))
            : Configuration(("Catalog:UseMockData", "false"), (ConnectionStringKey, connectionString));

        var ex = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddCatalogServices(configuration));

        Assert.Contains("ConnectionStrings:CatalogDBContext", ex.Message);
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();
}
