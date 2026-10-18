using eShopMvc.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShopMvc.Web.Controllers;

public class PicController : Controller
{
    public const string GetPicRouteName = "GetPicRouteTemplate";

    private readonly ICatalogService service;
    private readonly IWebHostEnvironment environment;
    private readonly ILogger<PicController> logger;

    public PicController(ICatalogService service, IWebHostEnvironment environment, ILogger<PicController> logger)
    {
        this.service = service;
        this.environment = environment;
        this.logger = logger;
    }

    // GET: items/5/pic
    [HttpGet("items/{catalogItemId:int}/pic", Name = GetPicRouteName)]
    public IActionResult Index(int catalogItemId)
    {
        logger.LogInformation("Now loading... /items/Index?{CatalogItemId}/pic", catalogItemId);

        if (catalogItemId <= 0)
        {
            return BadRequest();
        }

        var item = service.FindCatalogItem(catalogItemId);

        if (item != null)
        {
            var folder = Path.Combine(environment.ContentRootPath, PictureFiles.FolderName);
            if (!PictureFiles.TryResolvePath(folder, item.PictureFileName ?? string.Empty, out var path))
            {
                // Decision R-9, defense in depth: never serve a file outside the picture folder.
                logger.LogWarning("Refused picture path outside {Folder} for item {CatalogItemId}", PictureFiles.FolderName, catalogItemId);
                return NotFound();
            }

            var mimetype = PictureFiles.GetMimeType(Path.GetExtension(item.PictureFileName));

            // As in legacy, a missing file throws (500).
            return PhysicalFile(path, mimetype);
        }

        return NotFound();
    }
}
