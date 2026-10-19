using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace eShopMvc.Web.Services;

/// <summary>
/// Decision L3: <c>/health/live</c> (no dependencies) and <c>/health/ready</c> (checks tagged "ready", e.g. the
/// database in database mode). Both answer only Healthy/Unhealthy (200/503), no details.
/// </summary>
public static class HealthEndpoints
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";
    public const string ReadyTag = "ready";

    public static IEndpointRouteBuilder MapCatalogHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = _ => false });
        endpoints.MapHealthChecks(ReadyPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) });
        return endpoints;
    }
}
