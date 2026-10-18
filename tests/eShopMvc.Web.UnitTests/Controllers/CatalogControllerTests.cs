using eShopMvc.Web.Controllers;
using eShopMvc.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace eShopMvc.Web.UnitTests.Controllers;

// Decisions only; routing, antiforgery and views are covered by the integration and smoke tests.
public class CatalogControllerTests
{
    private readonly CatalogServiceMock service = new();

    [Fact]
    public void DeleteConfirmed_MissingId_ReturnsNotFoundAndDeletesNothing()
    {
        var controller = new CatalogController(service, NullLogger<CatalogController>.Instance);

        var result = controller.DeleteConfirmed(999);

        Assert.IsType<NotFoundResult>(result); // Decision R-15 (legacy: 500 in database mode)
        Assert.Equal(12, service.GetCatalogItemsPaginated(20, 0).TotalItems);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, -1)]
    [InlineData(int.MaxValue, 2)]
    public void Index_InvalidPaging_ReturnsBadRequest(int pageSize, int pageIndex)
    {
        var controller = new CatalogController(service, NullLogger<CatalogController>.Instance);

        Assert.IsType<BadRequestResult>(controller.Index(pageSize, pageIndex)); // Decision R-16
    }
}
