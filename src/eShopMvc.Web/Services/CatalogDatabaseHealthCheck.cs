using eShopMvc.Web.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace eShopMvc.Web.Services;

/// <summary>Readiness check for database mode (Decision L3): can the app connect to the catalog database?</summary>
public sealed class CatalogDatabaseHealthCheck(CatalogDBContext db) : IHealthCheck
{
    public const string Name = "catalog-database";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // CanConnectAsync returns false instead of throwing when the server or database is unavailable.
        return await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : new HealthCheckResult(context.Registration.FailureStatus);
    }
}
