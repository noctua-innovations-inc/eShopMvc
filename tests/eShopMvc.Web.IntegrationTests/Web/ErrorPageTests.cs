using System.Net;
using eShopMvc.Web.Models;
using eShopMvc.Web.Services;
using eShopMvc.Web.ViewModel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace eShopMvc.Web.IntegrationTests.Web;

/// <summary>
/// Decision R-23 outside Development: the exception handler's error page and empty 400/404 bodies.
/// The app runs in-process in the Production environment, in mock mode, with a catalog service whose
/// list call throws so that "/" produces an unhandled exception.
/// </summary>
public sealed class ErrorPageTests(ErrorPageTests.ProductionAppFactory factory) : IClassFixture<ErrorPageTests.ProductionAppFactory>
{
    public const string SecretExceptionMessage = "boom: secret-detail-from-the-exception";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UnhandledException_ShowsLegacyTextAndRequestIdInsideTheLayout_WithoutDetails_AndIsLogged()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/", Token);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(Token);
        Assert.Contains("<h1>Error.</h1>", html);
        Assert.Contains("<h2>An error occurred while processing your request.</h2>", html);
        Assert.Matches(@"<strong>Request ID:</strong> <code>[0-9a-f-]+</code>", html);
        Assert.Contains("Catalog manager <span>(MVC)</span>", html); // rendered inside the layout
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "<html")); // not a nested document
        Assert.DoesNotContain("secret-detail", html);
        Assert.DoesNotContain(nameof(InvalidOperationException), html);
        Assert.DoesNotContain("ThrowingCatalogService", html); // no stack trace
        Assert.Contains(factory.Logs.Snapshot(), e => e.Level == LogLevel.Error && e.Exception?.Message == SecretExceptionMessage);
    }

    [Theory]
    [InlineData("/Catalog/Details", HttpStatusCode.BadRequest)]
    [InlineData("/Catalog/Details/999", HttpStatusCode.NotFound)]
    [InlineData("/no/such/page", HttpStatusCode.NotFound)]
    [InlineData("/Pics/1.png", HttpStatusCode.NotFound)]
    public async Task BadRequestAndNotFound_HaveEmptyBodies(string path, HttpStatusCode expected)
    {
        // Legacy: IIS HTML error pages. New: status code only (Decision R-23).
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync(path, Token);

        Assert.Equal(expected, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(Token));
    }

    public sealed class ProductionAppFactory : WebApplicationFactory<Program>
    {
        public RecordingLoggerProvider Logs { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Catalog:UseMockData", "true");
            builder.ConfigureLogging(logging => logging.AddProvider(Logs));
            builder.ConfigureTestServices(services => services.AddSingleton<ICatalogService, ThrowingCatalogService>());
        }
    }

    private sealed class ThrowingCatalogService : ICatalogService
    {
        public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex) =>
            throw new InvalidOperationException(SecretExceptionMessage);

        public CatalogItem? FindCatalogItem(int id) => null;
        public IEnumerable<CatalogBrand> GetCatalogBrands() => [];
        public IEnumerable<CatalogType> GetCatalogTypes() => [];
        public void CreateCatalogItem(CatalogItem catalogItem) => throw new NotSupportedException();
        public void UpdateCatalogItem(CatalogItem catalogItem) => throw new NotSupportedException();
        public void RemoveCatalogItem(CatalogItem catalogItem) => throw new NotSupportedException();
    }

    public sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly List<(LogLevel Level, Exception? Exception)> entries = [];

        public IReadOnlyList<(LogLevel Level, Exception? Exception)> Snapshot()
        {
            lock (entries)
            {
                return [.. entries];
            }
        }

        public ILogger CreateLogger(string categoryName) => new Logger(this);

        public void Dispose()
        {
        }

        private sealed class Logger(RecordingLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (provider.entries)
                {
                    provider.entries.Add((logLevel, exception));
                }
            }
        }
    }
}
