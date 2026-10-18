using eShopMvc.Web.Models;
using eShopMvc.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShopMvc.Web.Controllers.WebApi;

// Legacy: Web API 2 convention route api/{controller}/{id}. JSON only, PascalCase (Decision R-7).
[ApiController]
[Route("api/brands")]
public class BrandsController : ControllerBase
{
    private readonly ICatalogService _service;

    public BrandsController(ICatalogService service)
    {
        _service = service;
    }

    // GET api/brands
    [HttpGet]
    public IEnumerable<CatalogBrand> Get()
    {
        return _service.GetCatalogBrands().ToList();
    }

    // GET api/brands/5
    [HttpGet("{id}")]
    public ActionResult<CatalogBrand> Get(int id)
    {
        var brand = _service.GetCatalogBrands().FirstOrDefault(x => x.Id == id);
        if (brand == null) return NotFound();

        return Ok(brand);
    }

    // DELETE api/brands/5
    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
        var brandToDelete = _service.GetCatalogBrands().FirstOrDefault(x => x.Id == id);
        if (brandToDelete == null)
        {
            return NotFound();
        }

        // demo only - don't actually delete
        return Ok();
    }
}
