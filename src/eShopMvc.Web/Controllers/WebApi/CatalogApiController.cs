using eShopMvc.Web.Infrastructure;
using eShopMvc.Web.Models;
using eShopMvc.Web.Services;
using eShopMvc.Web.ViewModel;
using Microsoft.AspNetCore.Mvc;

namespace eShopMvc.Web.Controllers.WebApi;

// Decision P2-6: JSON catalog API for the React UI, one action per Razor CatalogController action.
// PascalCase (R-7); errors as Problem Details ([ApiController]).
[ApiController]
[Route("api/v1/catalog")]
[AutoValidateAntiforgeryToken] // Decision P2-7: POST, PUT and DELETE need the X-XSRF-TOKEN header.
[TypeFilter<AntiforgeryCookieFilter>]
public class CatalogApiController : ControllerBase
{
    public const string GetItemRouteName = "GetCatalogApiItem";

    private readonly ICatalogService service;
    private readonly ILogger<CatalogApiController> logger;

    public CatalogApiController(ICatalogService service, ILogger<CatalogApiController> logger)
    {
        this.service = service;
        this.logger = logger;
    }

    // GET api/v1/catalog/items[?pageSize=10&pageIndex=0]
    [HttpGet("items")]
    public ActionResult<PaginatedItemsViewModel<CatalogItem>> GetItems(int pageSize = 10, int pageIndex = 0)
    {
        logger.LogInformation("Now loading... /api/v1/catalog/items?pageSize={PageSize}&pageIndex={PageIndex}", pageSize, pageIndex);
        if (!Paging.IsValid(pageSize, pageIndex))
        {
            return BadRequest(); // Decision R-16
        }

        var paginatedItems = service.GetCatalogItemsPaginated(pageSize, pageIndex);
        foreach (var catalogItem in paginatedItems.Data)
        {
            AddPictureUri(catalogItem);
        }
        return paginatedItems;
    }

    // GET api/v1/catalog/items/5
    [HttpGet("items/{id:int}", Name = GetItemRouteName)]
    public ActionResult<CatalogItem> GetItem(int id)
    {
        logger.LogInformation("Now loading... /api/v1/catalog/items/{Id}", id);
        var catalogItem = service.FindCatalogItem(id);
        if (catalogItem == null)
        {
            return NotFound();
        }
        AddPictureUri(catalogItem);
        return catalogItem;
    }

    // POST api/v1/catalog/items
    [HttpPost("items")]
    public ActionResult<CatalogItem> CreateItem(CatalogItem input)
    {
        logger.LogInformation("Now processing... POST /api/v1/catalog/items?catalogItemName={CatalogItemName}", input.Name);
        if (!BrandAndTypeExist(input))
        {
            return ValidationProblem(ModelState);
        }

        var catalogItem = new CatalogItem();
        CatalogItemFields.CopyEditable(input, catalogItem);
        service.CreateCatalogItem(catalogItem);

        // Read back, so the response has the brand and type like GET.
        var created = service.FindCatalogItem(catalogItem.Id) ?? catalogItem;
        AddPictureUri(created);
        return CreatedAtRoute(GetItemRouteName, new { id = created.Id }, created);
    }

    // PUT api/v1/catalog/items/5
    [HttpPut("items/{id:int}")]
    public IActionResult UpdateItem(int id, CatalogItem input)
    {
        logger.LogInformation("Now processing... PUT /api/v1/catalog/items/{Id}", id);
        if (input.Id != id)
        {
            // Not a field error the form could show, so plain Problem Details (ModelState already has "id" from the route).
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "The Id in the body must match the Id in the URL.");
        }

        var catalogItem = service.FindCatalogItem(id);
        if (catalogItem == null)
        {
            return NotFound();
        }
        if (!BrandAndTypeExist(input))
        {
            return ValidationProblem(ModelState);
        }

        // The stored instance is updated, not a new one: the EF service already tracks it after the lookup.
        CatalogItemFields.CopyEditable(input, catalogItem);
        service.UpdateCatalogItem(catalogItem);
        return NoContent();
    }

    // DELETE api/v1/catalog/items/5
    [HttpDelete("items/{id:int}")]
    public IActionResult DeleteItem(int id)
    {
        logger.LogInformation("Now processing... DELETE /api/v1/catalog/items/{Id}", id);
        var catalogItem = service.FindCatalogItem(id);
        if (catalogItem == null)
        {
            return NotFound(); // Decision R-15
        }
        service.RemoveCatalogItem(catalogItem);
        return NoContent();
    }

    // GET api/v1/catalog/brands
    [HttpGet("brands")]
    public IEnumerable<CatalogBrand> GetBrands()
    {
        logger.LogInformation("Now loading... /api/v1/catalog/brands");
        return service.GetCatalogBrands().ToList();
    }

    // GET api/v1/catalog/types
    [HttpGet("types")]
    public IEnumerable<CatalogType> GetTypes()
    {
        logger.LogInformation("Now loading... /api/v1/catalog/types");
        return service.GetCatalogTypes().ToList();
    }

    // Decision P2-13: unknown ids are a validation error (the Razor drop-downs never sent one; the
    // services would throw, 500).
    private bool BrandAndTypeExist(CatalogItem input)
    {
        if (!service.GetCatalogBrands().Any(b => b.Id == input.CatalogBrandId))
        {
            ModelState.AddModelError(nameof(CatalogItem.CatalogBrandId), "The selected brand does not exist.");
        }
        if (!service.GetCatalogTypes().Any(t => t.Id == input.CatalogTypeId))
        {
            ModelState.AddModelError(nameof(CatalogItem.CatalogTypeId), "The selected type does not exist.");
        }
        return ModelState.IsValid;
    }

    private void AddPictureUri(CatalogItem item)
    {
        item.PictureUri = Url.RouteUrl(PicController.GetPicRouteName, new { catalogItemId = item.Id }, Request.Scheme);
    }
}
