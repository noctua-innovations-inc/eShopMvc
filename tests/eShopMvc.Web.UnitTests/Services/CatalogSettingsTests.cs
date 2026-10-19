using eShopMvc.Web.Services;
using Microsoft.Extensions.Configuration;

namespace eShopMvc.Web.UnitTests.Services;

public class CatalogSettingsTests
{
    private const string Connection = "ConnectionStrings:CatalogDBContext";
    private const string ValidConnectionString = @"Server=(localdb)\MSSQLLocalDB;Database=x;Integrated Security=True";

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData(null, false)] // default: database mode (Decision R-2)
    public void Validate_ValidSettings_ReturnsOptions(string? useMockData, bool expected)
    {
        var settings = new Dictionary<string, string?> { [Connection] = ValidConnectionString };
        if (useMockData is not null)
        {
            settings["Catalog:UseMockData"] = useMockData;
        }

        Assert.Equal(expected, CatalogSettings.Validate(Configuration(settings)).UseMockData);
    }

    [Fact]
    public void Validate_MockModeWithoutConnectionString_IsValid()
    {
        Assert.True(CatalogSettings.Validate(Configuration(new() { ["Catalog:UseMockData"] = "true" })).UseMockData);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_DatabaseModeWithoutConnectionString_NamesTheSettingAndItsEnvironmentVariable(string? connectionString)
    {
        var ex = Invalid(new() { ["Catalog:UseMockData"] = "false", [Connection] = connectionString });

        Assert.Contains("ConnectionStrings:CatalogDBContext is not set", ex.Message);
        Assert.Contains("ConnectionStrings__CatalogDBContext", ex.Message);
    }

    [Fact]
    public void Validate_UnparseableConnectionString_FailsWithoutEchoingIt()
    {
        var ex = Invalid(new() { [Connection] = "Serverr=db;Password=secret-value" });

        Assert.Contains("ConnectionStrings:CatalogDBContext is not a valid SQL Server connection string", ex.Message);
        Assert.DoesNotContain("secret-value", ex.Message);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("1")]
    [InlineData("")]
    public void Validate_UseMockDataNotABoolean_NamesKeyAndValue(string value)
    {
        var ex = Invalid(new() { ["Catalog:UseMockData"] = value, [Connection] = ValidConnectionString });

        Assert.Contains($"Catalog:UseMockData must be 'true' or 'false', but is '{value}'.", ex.Message);
    }

    [Fact]
    public void Validate_MisspelledKey_IsRejected()
    {
        var ex = Invalid(new() { ["Catalog:UseMockDat"] = "true", [Connection] = ValidConnectionString });

        Assert.Contains("Catalog:UseMockDat is not a known setting", ex.Message);
    }

    [Fact]
    public void Validate_UseCustomizationData_PointsToDecisionD10()
    {
        var ex = Invalid(new() { ["Catalog:UseCustomizationData"] = "false", [Connection] = ValidConnectionString });

        Assert.Contains("Catalog:UseCustomizationData is not supported", ex.Message);
        Assert.Contains("D10", ex.Message);
    }

    [Fact]
    public void Validate_SeveralProblems_ReportsAllInOneMessage()
    {
        var ex = Invalid(new() { ["Catalog:UseMockData"] = "false", ["Catalog:Typo"] = "x", ["Catalog:UseCustomizationData"] = "true" });

        Assert.StartsWith("Invalid configuration:", ex.Message);
        Assert.Contains("Catalog:Typo", ex.Message);
        Assert.Contains("Catalog:UseCustomizationData", ex.Message);
        Assert.Contains("ConnectionStrings:CatalogDBContext is not set", ex.Message);
    }

    private static InvalidOperationException Invalid(Dictionary<string, string?> settings) =>
        Assert.Throws<InvalidOperationException>(() => CatalogSettings.Validate(Configuration(settings)));

    private static IConfiguration Configuration(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
}
