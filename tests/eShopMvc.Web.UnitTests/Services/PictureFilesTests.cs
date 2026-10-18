using eShopMvc.Web.Models;
using eShopMvc.Web.Models.Infrastructure;
using eShopMvc.Web.Services;

namespace eShopMvc.Web.UnitTests.Services;

public class PictureFilesTests
{
    // Only used as a string: TryResolvePath doesn't touch the file system.
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "eshop-pics");

    [Theory]
    [InlineData("1.png")]
    [InlineData("dummy.png")]
    [InlineData("my-picture_2.JPEG")]
    [InlineData("..backup.png")]
    [InlineData("a..b")]
    [InlineData(".hidden")]
    [InlineData(null)]
    [InlineData("")]
    public void IsValidFileName_AllowlistedOrEmpty_IsValid(string? fileName)
    {
        Assert.True(PictureFiles.IsValidFileName(fileName));
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData(@"..\Web.config")]
    [InlineData("../Web.config")]
    [InlineData(@"sub\1.png")]
    [InlineData("sub/1.png")]
    [InlineData(@"C:\Windows\win.ini")]
    [InlineData("/etc/passwd")]
    [InlineData("C:1.png")]
    [InlineData("1.png ")]
    [InlineData("my picture.png")]
    [InlineData("1.png\0.txt")]
    [InlineData("caf\u00e9.png")]
    [InlineData("1.png?x")]
    public void IsValidFileName_PathOrDisallowedCharacters_IsInvalid(string fileName)
    {
        Assert.False(PictureFiles.IsValidFileName(fileName));
    }

    [Fact]
    public void IsValidFileName_AllLegacyPictureNames_AreValid()
    {
        var names = PreconfiguredData.GetPreconfiguredCatalogItems().Select(i => i.PictureFileName)
            .Append(CatalogItem.DefaultPictureName)
            .Concat(Directory.GetFiles(WebProjectPicsFolder()).Select(Path.GetFileName))
            .ToList();

        Assert.Equal(13, names.Distinct().Count()); // 1.png-12.png and dummy.png
        Assert.All(names, name => Assert.True(PictureFiles.IsValidFileName(name), name));
    }

    [Fact]
    public void PictureFileNameAttribute_InvalidName_FailsValidationNamingTheField()
    {
        var item = new CatalogItem { Name = "x", PictureFileName = @"..\Web.config" };
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            item, new(item), results, validateAllProperties: true);

        Assert.False(valid);
        var result = Assert.Single(results);
        Assert.Equal([nameof(CatalogItem.PictureFileName)], result.MemberNames);
        Assert.StartsWith("The field Picture name must be a file name", result.ErrorMessage);
    }

    [Theory]
    [InlineData("1.png")]
    [InlineData("..backup.png")] // starts with two dots, but is a file name inside the folder
    [InlineData("a..b.png")]
    public void TryResolvePath_NameInsideFolder_ResolvesUnderFolder(string fileName)
    {
        Assert.True(PictureFiles.TryResolvePath(Folder, fileName, out var path));
        Assert.Equal(Path.Combine(Path.GetFullPath(Folder), fileName), path);
    }

    [Fact]
    public void TryResolvePath_SubfolderThatComesBack_IsInside()
    {
        Assert.True(PictureFiles.TryResolvePath(Folder, Path.Combine("sub", "..", "1.png"), out var path));
        Assert.Equal(Path.Combine(Path.GetFullPath(Folder), "1.png"), path);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../x.png")]
    [InlineData("../../x.png")]
    [InlineData("sub/../../x.png")]
    public void TryResolvePath_NameLeavingFolder_IsOutside(string fileName)
    {
        Assert.False(PictureFiles.TryResolvePath(Folder, fileName, out _));
    }

    [Fact]
    public void TryResolvePath_BackslashParent_IsOutsideOnWindowsAndAFileNameElsewhere()
    {
        // On Windows '\' is a separator, so ..\x.png leaves the folder. On Linux it's an ordinary
        // character and ..\x.png is a (strange) file name inside the folder.
        Assert.Equal(!OperatingSystem.IsWindows(), PictureFiles.TryResolvePath(Folder, @"..\x.png", out _));
    }

    [Fact]
    public void TryResolvePath_AbsolutePath_IsOutside()
    {
        var absolute = OperatingSystem.IsWindows() ? @"C:\Windows\win.ini" : "/etc/passwd";

        Assert.False(PictureFiles.TryResolvePath(Folder, absolute, out _));
    }

    [Theory]
    [InlineData(".png", "image/png")]
    [InlineData(".gif", "image/gif")]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".bmp", "image/bmp")]
    [InlineData(".tiff", "image/tiff")]
    [InlineData(".wmf", "image/wmf")]
    [InlineData(".jp2", "image/jp2")]
    [InlineData(".svg", "image/svg+xml")]
    [InlineData(".PNG", "application/octet-stream")] // legacy switch is case-sensitive
    [InlineData(".config", "application/octet-stream")]
    [InlineData("", "application/octet-stream")]
    [InlineData(null, "application/octet-stream")]
    public void GetMimeType_MatchesLegacyMapping(string? extension, string expected)
    {
        Assert.Equal(expected, PictureFiles.GetMimeType(extension));
    }

    private static string WebProjectPicsFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "eShopMvc.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "src", "eShopMvc.Web", PictureFiles.FolderName);
    }
}
