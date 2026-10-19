using eShopMvc.Web.IntegrationTests.Infrastructure;
using eShopMvc.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eShopMvc.Web.IntegrationTests.Services;

public class CatalogDatabaseStartupTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HealthCheck_TestDatabase_IsHealthy()
    {
        await using var context = TestDatabase.CreateContext();

        var result = await new CatalogDatabaseHealthCheck(context).CheckHealthAsync(HealthContext(), Token);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task HealthCheck_DatabaseThatDoesNotExist_IsUnhealthy()
    {
        await using var context = TestDatabase.CreateContext(TestDatabase.WithDatabaseSuffix(".DoesNotExist"));

        var result = await new CatalogDatabaseHealthCheck(context).CheckHealthAsync(HealthContext(), Token);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task InitializeCatalogDatabase_UnreachableServer_LogsCriticalWithoutPasswordAndRethrows()
    {
        // Port 1 on loopback refuses connections quickly; the password must never reach the log.
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Catalog:UseMockData"] = "false",
            ["ConnectionStrings:CatalogDBContext"] = "Server=tcp:127.0.0.1,1;Database=eShopMvc.Unreachable;User ID=sa;Password=secret-value;Connect Timeout=2;Encrypt=False",
        });
        builder.Logging.ClearProviders();
        var logs = new RecordingLoggerProvider();
        builder.Logging.AddProvider(logs);
        builder.Services.AddCatalogServices(builder.Configuration);
        using var host = builder.Build();

        await Assert.ThrowsAnyAsync<Exception>(host.InitializeCatalogDatabaseAsync);

        var critical = Assert.Single(logs.Entries, e => e.Level == LogLevel.Critical);
        Assert.Contains("eShopMvc.Unreachable", critical.Message);
        Assert.Contains("tcp:127.0.0.1,1", critical.Message);
        Assert.Contains("ConnectionStrings__CatalogDBContext", critical.Message);
        Assert.DoesNotContain(logs.Entries, e => e.Message.Contains("secret-value"));
    }

    private static HealthCheckContext HealthContext() => new()
    {
        Registration = new HealthCheckRegistration(CatalogDatabaseHealthCheck.Name, _ => null!, HealthStatus.Unhealthy, ["ready"]),
    };

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

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
                lock (provider.Entries)
                {
                    provider.Entries.Add((logLevel, formatter(state, exception)));
                }
            }
        }
    }
}
