using eShopMvc.SmokeTests.Infrastructure;

[assembly: AssemblyFixture(typeof(SuiteFixture))]

namespace eShopMvc.SmokeTests.Infrastructure;

/// <summary>
/// Runs once before any test: validates the settings, fails fast with a clear message if the app
/// isn't reachable, and deletes <c>smoke-*</c> items left behind by an aborted earlier run.
/// Seeded items are never touched.
/// </summary>
public sealed class SuiteFixture : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        var baseUrl = SmokeSettings.BaseUrl;
        var target = SmokeSettings.Target;

        using var client = new AppClient();
        try
        {
            using var response = await client.GetAsync("/");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                $"The app under test is not reachable at {baseUrl} (target: {target}). " +
                $"Start it first (legacy: .\\run-legacy.ps1), or set {SmokeSettings.BaseUrlVariable}. {ex.Message}",
                ex);
        }

        // Decision P2-12: the new app's pages are React since the cutover, so its items go through the JSON API.
        if (SmokeSettings.IsNew)
        {
            foreach (var leftover in (await CatalogApi.AllItemsAsync(client)).Where(i => i.Name.StartsWith(CatalogUi.SmokePrefix, StringComparison.Ordinal)))
            {
                await CatalogApi.DeleteAsync(client, leftover.Id);
            }

            return;
        }

        foreach (var leftover in (await CatalogUi.AllRowsAsync(client)).Where(r => r.Name.StartsWith(CatalogUi.SmokePrefix, StringComparison.Ordinal)))
        {
            await CatalogUi.DeleteAsync(client, leftover.Id);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
