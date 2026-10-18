namespace eShopMvc.Web.Models;

// Nested in the legacy FilesController and [Serializable] for BinaryFormatter.
// Top-level here and serialized as JSON by /api/v1/files (Decisions R-1, D4).
public class BrandDTO
{
    public int Id { get; set; }
    public string? Brand { get; set; }
}
