using eShopMvc.Web.Controllers.WebApi;
using eShopMvc.Web.Models;
using eShopMvc.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace eShopMvc.Web.UnitTests.Controllers;

// Decisions only; routing, JSON, antiforgery and the success paths are covered by the integration and smoke tests.
public class CatalogApiControllerTests
{
    private const int SeededItems = 12;

    private readonly CatalogServiceMock service = new();

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, -1)]
    [InlineData(int.MaxValue, 2)]
    public void GetItems_InvalidPaging_ReturnsBadRequest(int pageSize, int pageIndex)
    {
        var controller = CreateController();

        Assert.IsType<BadRequestResult>(controller.GetItems(pageSize, pageIndex).Result); // Decision R-16
    }

    [Fact]
    public void GetItem_MissingId_ReturnsNotFound()
    {
        var controller = CreateController();

        Assert.IsType<NotFoundResult>(controller.GetItem(999).Result);
    }

    [Fact]
    public void CreateItem_UnknownBrandAndType_ReturnsValidationProblemAndCreatesNothing()
    {
        var controller = CreateController();

        var result = controller.CreateItem(ValidItem(brandId: 999, typeId: 999)).Result;

        var errors = AssertValidationProblem(result);
        Assert.Equal(["CatalogBrandId", "CatalogTypeId"], errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(SeededItems, service.GetCatalogItemsPaginated(20, 0).TotalItems); // Decision P2-13
    }

    [Fact]
    public void UpdateItem_BodyIdDiffersFromUrl_ReturnsBadRequestProblem()
    {
        var controller = CreateController();
        var input = ValidItem();
        input.Id = 2;

        var result = Assert.IsType<ObjectResult>(controller.UpdateItem(1, input));

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.IsNotType<ValidationProblemDetails>(Assert.IsType<ProblemDetails>(result.Value, exactMatch: false));
        Assert.NotEqual(input.Name, service.FindCatalogItem(1)!.Name);
    }

    [Fact]
    public void UpdateItem_MissingId_ReturnsNotFound()
    {
        var controller = CreateController();
        var input = ValidItem();
        input.Id = 999;

        Assert.IsType<NotFoundResult>(controller.UpdateItem(999, input));
    }

    [Fact]
    public void UpdateItem_UnknownType_ReturnsValidationProblemAndChangesNothing()
    {
        var controller = CreateController();
        var input = ValidItem(typeId: 999);
        input.Id = 1;

        var errors = AssertValidationProblem(controller.UpdateItem(1, input));

        Assert.Equal(["CatalogTypeId"], errors.Keys);
        Assert.NotEqual(input.Name, service.FindCatalogItem(1)!.Name); // Decision P2-13
    }

    [Fact]
    public void UpdateItem_Valid_UpdatesTheStoredItemAndReturnsNoContent()
    {
        var controller = CreateController();
        var input = ValidItem();
        input.Id = 1;

        Assert.IsType<NoContentResult>(controller.UpdateItem(1, input));

        var stored = service.FindCatalogItem(1)!;
        Assert.Equal(input.Name, stored.Name);
        Assert.Equal(input.Price, stored.Price);
    }

    [Fact]
    public void DeleteItem_MissingId_ReturnsNotFoundAndDeletesNothing()
    {
        var controller = CreateController();

        Assert.IsType<NotFoundResult>(controller.DeleteItem(999)); // Decision R-15
        Assert.Equal(SeededItems, service.GetCatalogItemsPaginated(20, 0).TotalItems);
    }

    [Fact]
    public void DeleteItem_Existing_RemovesItAndReturnsNoContent()
    {
        var controller = CreateController();

        Assert.IsType<NoContentResult>(controller.DeleteItem(1));
        Assert.Null(service.FindCatalogItem(1));
    }

    private CatalogApiController CreateController()
    {
        // ValidationProblem() needs the ProblemDetailsFactory from the request services.
        var services = new ServiceCollection().AddLogging().AddControllers().Services.BuildServiceProvider();
        return new CatalogApiController(service, NullLogger<CatalogApiController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services },
            },
        };
    }

    private static CatalogItem ValidItem(int brandId = 1, int typeId = 1) => new()
    {
        Name = "unit-test item",
        Price = 12.34m,
        CatalogBrandId = brandId,
        CatalogTypeId = typeId,
    };

    private static IDictionary<string, string[]> AssertValidationProblem(object? result)
    {
        var objectResult = Assert.IsType<ObjectResult>(result, exactMatch: false);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        return Assert.IsType<ValidationProblemDetails>(objectResult.Value).Errors;
    }
}
