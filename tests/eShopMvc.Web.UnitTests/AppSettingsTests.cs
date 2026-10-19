using Microsoft.Extensions.Configuration;

namespace eShopMvc.Web.UnitTests;

// Pins the step-8 logging decisions in the shipped appsettings files (and that both files parse).
public class AppSettingsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LogLevels_HideFrameworkNoiseAndKeepAppMessages(bool development)
    {
        var config = Load(development);

        Assert.Equal("Information", config["Logging:LogLevel:Default"]); // includes eShopMvc.* (legacy action log lines)
        Assert.Equal("Warning", config["Logging:LogLevel:Microsoft.AspNetCore"]);
        Assert.Equal("Warning", config["Logging:LogLevel:Microsoft.EntityFrameworkCore"]); // no SQL command per query
        Assert.Equal("Information", config["Logging:LogLevel:Microsoft.EntityFrameworkCore.Migrations"]);
        Assert.Equal("Information", config["Logging:LogLevel:Microsoft.Hosting.Lifetime"]);
        Assert.Equal("true", config["Logging:Console:FormatterOptions:IncludeScopes"], ignoreCase: true); // Decision L2
    }

    [Theory]
    [InlineData(false, "json")] // Decision L1: structured output for container/Azure log collection
    [InlineData(true, "simple")] // readable single lines locally
    public void ConsoleFormatter_JsonExceptInDevelopment(bool development, string expected)
    {
        Assert.Equal(expected, Load(development)["Logging:Console:FormatterName"]);
    }

    [Fact]
    public void ConnectionString_OnlyInDevelopmentSettings()
    {
        // Decision D5: other environments must supply ConnectionStrings__CatalogDBContext.
        Assert.Null(Load(development: false)["ConnectionStrings:CatalogDBContext"]);
        Assert.Contains("eShopMvc.CatalogDb", Load(development: true)["ConnectionStrings:CatalogDBContext"]);
    }

    private static IConfiguration Load(bool development)
    {
        var builder = new ConfigurationBuilder().SetBasePath(WebProjectFolder()).AddJsonFile("appsettings.json");
        if (development)
        {
            builder.AddJsonFile("appsettings.Development.json");
        }

        return builder.Build();
    }

    private static string WebProjectFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "eShopMvc.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "src", "eShopMvc.Web");
    }
}
