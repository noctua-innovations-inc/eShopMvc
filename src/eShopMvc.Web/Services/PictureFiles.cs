using System.Text.RegularExpressions;

namespace eShopMvc.Web.Services;

/// <summary>Catalog picture file names, paths and MIME types (legacy: PicController; Decision R-9).</summary>
public static partial class PictureFiles
{
    /// <summary>The picture folder, relative to the content root (legacy: <c>~/Pics</c>).</summary>
    public const string FolderName = "Pics";

    /// <summary>
    /// Allowlist (Decision R-9): a bare file name of ASCII letters, digits, <c>-</c>, <c>_</c> and <c>.</c>,
    /// other than <c>.</c> and <c>..</c>. Null or empty is not this rule's concern.
    /// </summary>
    public static bool IsValidFileName(string? fileName) =>
        string.IsNullOrEmpty(fileName)
        || (fileName != "." && fileName != ".." && AllowedFileName().IsMatch(fileName));

    /// <summary>
    /// Resolves <paramref name="fileName"/> under <paramref name="folder"/>. Returns false if the result is
    /// outside the folder (defense in depth for names that bypassed validation, Decision R-9).
    /// </summary>
    public static bool TryResolvePath(string folder, string fileName, out string path)
    {
        var root = Path.GetFullPath(folder);
        path = Path.GetFullPath(Path.Combine(root, fileName));

        var relative = Path.GetRelativePath(root, path);
        var outside = relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative);
        return !outside;
    }

    /// <summary>Legacy mapping, unchanged: case-sensitive, unknown extensions are <c>application/octet-stream</c>.</summary>
    public static string GetMimeType(string? extension) => extension switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".bmp" => "image/bmp",
        ".tiff" => "image/tiff",
        ".wmf" => "image/wmf",
        ".jp2" => "image/jp2",
        ".svg" => "image/svg+xml",
        _ => "application/octet-stream",
    };

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex AllowedFileName();
}
