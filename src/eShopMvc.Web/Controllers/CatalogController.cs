using eShopMvc.Web.Models;
using eShopMvc.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace eShopMvc.Web.Controllers;

public class CatalogController : Controller
{
    private const string BindFields = "Id,Name,Description,Price,PictureFileName,CatalogTypeId,CatalogBrandId,AvailableStock,RestockThreshold,MaxStockThreshold,OnReorder";

    private readonly ICatalogService service;
    private readonly ILogger<CatalogController> logger;

    public CatalogController(ICatalogService service, ILogger<CatalogController> logger)
    {
        this.service = service;
        this.logger = logger;
    }

    // GET /[?pageSize=3&pageIndex=10]
    public IActionResult Index(int pageSize = 10, int pageIndex = 0)
    {
        logger.LogInformation("Now loading... /Catalog/Index?pageSize={PageSize}&pageIndex={PageIndex}", pageSize, pageIndex);
        if (!Paging.IsValid(pageSize, pageIndex))
        {
            return BadRequest(); // Decision R-16 (legacy: 500)
        }

        var paginatedItems = service.GetCatalogItemsPaginated(pageSize, pageIndex);
        ChangeUriPlaceholder(paginatedItems.Data);
        return View(paginatedItems);
    }

    // GET: Catalog/Details/5
    public IActionResult Details(int? id)
    {
        logger.LogInformation("Now loading... /Catalog/Details?id={Id}", id);
        if (id == null)
        {
            return BadRequest();
        }
        var catalogItem = service.FindCatalogItem(id.Value);
        if (catalogItem == null)
        {
            return NotFound();
        }
        AddUriPlaceHolder(catalogItem);

        return View(catalogItem);
    }

    // GET: Catalog/Create
    public IActionResult Create()
    {
        logger.LogInformation("Now loading... /Catalog/Create");
        ViewBag.CatalogBrandId = new SelectList(service.GetCatalogBrands(), "Id", "Brand");
        ViewBag.CatalogTypeId = new SelectList(service.GetCatalogTypes(), "Id", "Type");
        return View(new CatalogItem());
    }

    // POST: Catalog/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create([Bind(BindFields)] CatalogItem catalogItem)
    {
        logger.LogInformation("Now processing... /Catalog/Create?catalogItemName={CatalogItemName}", catalogItem.Name);
        if (ModelState.IsValid)
        {
            service.CreateCatalogItem(catalogItem);
            return RedirectToAction("Index");
        }

        ViewBag.CatalogBrandId = new SelectList(service.GetCatalogBrands(), "Id", "Brand", catalogItem.CatalogBrandId);
        ViewBag.CatalogTypeId = new SelectList(service.GetCatalogTypes(), "Id", "Type", catalogItem.CatalogTypeId);
        return View(catalogItem);
    }

    // GET: Catalog/Edit/5
    public IActionResult Edit(int? id)
    {
        logger.LogInformation("Now loading... /Catalog/Edit?id={Id}", id);
        if (id == null)
        {
            return BadRequest();
        }
        var catalogItem = service.FindCatalogItem(id.Value);
        if (catalogItem == null)
        {
            return NotFound();
        }
        AddUriPlaceHolder(catalogItem);
        ViewBag.CatalogBrandId = new SelectList(service.GetCatalogBrands(), "Id", "Brand", catalogItem.CatalogBrandId);
        ViewBag.CatalogTypeId = new SelectList(service.GetCatalogTypes(), "Id", "Type", catalogItem.CatalogTypeId);
        return View(catalogItem);
    }

    // POST: Catalog/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit([Bind(BindFields)] CatalogItem catalogItem)
    {
        logger.LogInformation("Now processing... /Catalog/Edit?id={Id}", catalogItem.Id);
        if (ModelState.IsValid)
        {
            service.UpdateCatalogItem(catalogItem);
            return RedirectToAction("Index");
        }
        AddUriPlaceHolder(catalogItem); // Decision R-24 (legacy re-rendered without the picture)
        ViewBag.CatalogBrandId = new SelectList(service.GetCatalogBrands(), "Id", "Brand", catalogItem.CatalogBrandId);
        ViewBag.CatalogTypeId = new SelectList(service.GetCatalogTypes(), "Id", "Type", catalogItem.CatalogTypeId);
        return View(catalogItem);
    }

    // GET: Catalog/Delete/5
    public IActionResult Delete(int? id)
    {
        logger.LogInformation("Now loading... /Catalog/Delete?id={Id}", id);
        if (id == null)
        {
            return BadRequest();
        }
        var catalogItem = service.FindCatalogItem(id.Value);
        if (catalogItem == null)
        {
            return NotFound();
        }
        AddUriPlaceHolder(catalogItem);

        return View(catalogItem);
    }

    // POST: Catalog/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed(int id)
    {
        logger.LogInformation("Now processing... /Catalog/DeleteConfirmed?id={Id}", id);
        var catalogItem = service.FindCatalogItem(id);
        if (catalogItem == null)
        {
            return NotFound(); // Decision R-15 (legacy: 500 in database mode)
        }
        service.RemoveCatalogItem(catalogItem);
        return RedirectToAction("Index");
    }

    private void ChangeUriPlaceholder(IEnumerable<CatalogItem> items)
    {
        foreach (var catalogItem in items)
        {
            AddUriPlaceHolder(catalogItem);
        }
    }

    private void AddUriPlaceHolder(CatalogItem item)
    {
        item.PictureUri = Url.RouteUrl(PicController.GetPicRouteName, new { catalogItemId = item.Id }, Request.Scheme);
    }
}
