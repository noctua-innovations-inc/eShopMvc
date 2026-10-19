using Microsoft.Data.SqlClient;

namespace eShopMvc.Web.Services;

/// <summary>
/// Validates the catalog configuration before the app is built (step 8), so misconfiguration stops startup
/// with one message listing every problem instead of failing on a request.
/// </summary>
public static class CatalogSettings
{
    /// <summary>Returns the validated options, or throws <see cref="InvalidOperationException"/> listing all errors.</summary>
    public static CatalogOptions Validate(IConfiguration configuration)
    {
        var errors = new List<string>();
        var section = configuration.GetSection(CatalogOptions.SectionName);
        var useMockDataKey = $"{CatalogOptions.SectionName}:{nameof(CatalogOptions.UseMockData)}";

        foreach (var child in section.GetChildren())
        {
            if (string.Equals(child.Key, "UseCustomizationData", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{CatalogOptions.SectionName}:{child.Key} is not supported: CSV/ZIP seeding was not ported (Decision D10). Remove the setting.");
            }
            else if (!string.Equals(child.Key, nameof(CatalogOptions.UseMockData), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{CatalogOptions.SectionName}:{child.Key} is not a known setting (known: {nameof(CatalogOptions.UseMockData)}). Check the spelling.");
            }
        }

        var useMockData = false;
        var rawUseMockData = section[nameof(CatalogOptions.UseMockData)];
        var useMockDataValid = rawUseMockData is null || bool.TryParse(rawUseMockData, out useMockData);
        if (!useMockDataValid)
        {
            errors.Add($"{useMockDataKey} must be 'true' or 'false', but is '{rawUseMockData}'.");
        }

        // Only check the database settings when the mode is known to be database mode.
        if (useMockDataValid && !useMockData)
        {
            var connectionStringKey = $"ConnectionStrings:{CatalogServiceCollectionExtensions.ConnectionStringName}";
            var connectionString = configuration.GetConnectionString(CatalogServiceCollectionExtensions.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                errors.Add(
                    $"{useMockDataKey} is false (database mode), but {connectionStringKey} is not set. Set it (e.g. environment " +
                    $"variable ConnectionStrings__{CatalogServiceCollectionExtensions.ConnectionStringName}), or set {useMockDataKey}=true.");
            }
            else
            {
                try
                {
                    _ = new SqlConnectionStringBuilder(connectionString);
                }
                catch (Exception ex) when (ex is ArgumentException or FormatException or KeyNotFoundException)
                {
                    // The value itself is never echoed: it may contain a password.
                    errors.Add($"{connectionStringKey} is not a valid SQL Server connection string: {ex.Message}");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Invalid configuration:" + string.Concat(errors.Select(e => Environment.NewLine + " - " + e)));
        }

        return new CatalogOptions { UseMockData = useMockData };
    }
}
