using eShopMvc.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eShopMvc.Web.UnitTests.Services;

public class CatalogServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCatalogServices_UseMockDataTrue_RegistersMockAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddCatalogServices(Configuration(("Catalog:UseMockData", "true")));

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ICatalogService));
        Assert.Equal(typeof(CatalogServiceMock), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Theory]
    [InlineData("false")]
    [InlineData(null)]
    public void AddCatalogServices_UseMockDataFalseOrMissing_ThrowsPointingToStep5(string? value)
    {
        var configuration = value is null
            ? Configuration()
            : Configuration(("Catalog:UseMockData", value));

        var ex = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddCatalogServices(configuration));

        Assert.Contains("Catalog:UseMockData", ex.Message);
        Assert.Contains("step 5", ex.Message);
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();
}
