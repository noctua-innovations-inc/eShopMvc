using System.ComponentModel.DataAnnotations;
using eShopMvc.Web.Services;

namespace eShopMvc.Web.Models;

/// <summary>
/// Rejects picture names that aren't a bare, allowlisted file name (Decision R-9), so Create and Edit
/// show a form validation error instead of storing a path. Null or empty is valid here.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class PictureFileNameAttribute : ValidationAttribute
{
    public PictureFileNameAttribute()
        : base("The field {0} must be a file name made of letters, digits, '-', '_' and '.'.")
    {
    }

    public override bool IsValid(object? value) => PictureFiles.IsValidFileName(value as string);
}
