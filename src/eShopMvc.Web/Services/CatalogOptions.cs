namespace eShopMvc.Web.Services;

/// <summary>The <c>Catalog</c> section of appsettings.json (legacy: Web.config appSettings).</summary>
public sealed class CatalogOptions
{
    public const string SectionName = "Catalog";

    /// <summary>True: in-memory mock data. False (default, Decision R-2): SQL Server via EF Core.</summary>
    public bool UseMockData { get; set; }
}
