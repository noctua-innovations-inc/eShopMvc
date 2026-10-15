namespace eShopMvc.SmokeTests;

/// <summary>Which application the suite is running against.</summary>
public enum TargetApp
{
    /// <summary>The legacy ASP.NET MVC 5 app (eShopLegacyMVC).</summary>
    Legacy,

    /// <summary>The ported ASP.NET Core app (eShopMvc.Web).</summary>
    New,
}

/// <summary>
/// Settings read from environment variables:
/// <c>ESHOP_BASE_URL</c> (default <c>http://localhost:5050</c>) and
/// <c>ESHOP_TARGET</c> = <c>legacy</c> | <c>new</c> (default <c>legacy</c>).
/// </summary>
public static class SmokeSettings
{
    public const string BaseUrlVariable = "ESHOP_BASE_URL";
    public const string TargetVariable = "ESHOP_TARGET";

    public static Uri BaseUrl { get; } = ReadBaseUrl();

    public static TargetApp Target { get; } = ReadTarget();

    // Used by [Fact(SkipUnless = ...)] on tests whose expected result differs per target (Decisions R-1, R-9).
    public static bool IsLegacy => Target == TargetApp.Legacy;

    public static bool IsNew => Target == TargetApp.New;

    private static Uri ReadBaseUrl()
    {
        var value = Environment.GetEnvironmentVariable(BaseUrlVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            value = "http://localhost:5050";
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"{BaseUrlVariable}='{value}' is not an absolute URL.");
        }

        return uri;
    }

    private static TargetApp ReadTarget()
    {
        var value = Environment.GetEnvironmentVariable(TargetVariable);
        return value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "legacy" => TargetApp.Legacy,
            "new" => TargetApp.New,
            _ => throw new InvalidOperationException($"{TargetVariable}='{value}' must be 'legacy' or 'new'."),
        };
    }
}
