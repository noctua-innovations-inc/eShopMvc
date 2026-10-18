using eShopMvc.Web.Models;
using eShopMvc.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShopMvc.Web.Controllers.WebApi;

// Decision R-1: legacy /api/files returned the brands as a BinaryFormatter payload, which .NET 10
// cannot produce. The same data is now JSON at /api/v1/files; /api/files answers 410 Gone.
[ApiController]
public class FilesController : ControllerBase
{
    public const string SuccessorPath = "/api/v1/files";

    private readonly ICatalogService _service;

    public FilesController(ICatalogService service)
    {
        _service = service;
    }

    // GET api/v1/files
    [HttpGet("api/v1/files")]
    public IEnumerable<BrandDTO> Get()
    {
        return _service.GetCatalogBrands()
            .Select(b => new BrandDTO
            {
                Id = b.Id,
                Brand = b.Brand
            }).ToList();
    }

    // GET api/files
    [HttpGet("api/files")]
    public IActionResult GetRetired()
    {
        Response.Headers.Link = $"<{SuccessorPath}>; rel=\"successor-version\"";
        return Problem(
            statusCode: StatusCodes.Status410Gone,
            title: "This endpoint has been retired.",
            detail: $"GET /api/files returned a BinaryFormatter payload, which is no longer supported. Use GET {SuccessorPath}, which returns the same brands as JSON.");
    }
}
