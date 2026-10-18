using System.Diagnostics;
using eShopMvc.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace eShopMvc.Web.Controllers;

// Target of UseExceptionHandler outside Development (Decision R-23). No HTTP method attribute:
// the handler re-executes the failed request's method, which may be a POST.
public class ErrorController : Controller
{
    [Route("Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index()
    {
        return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
