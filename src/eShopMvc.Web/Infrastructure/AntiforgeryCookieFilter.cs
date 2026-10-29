using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Filters;

namespace eShopMvc.Web.Infrastructure;

/// <summary>
/// Decision P2-7: on every GET, issues the antiforgery request token as a cookie that JavaScript can
/// read (<see cref="CookieName"/>). The React client sends it back in the <see cref="HeaderName"/>
/// header; <c>[AutoValidateAntiforgeryToken]</c> checks it on POST, PUT and DELETE.
/// </summary>
public sealed class AntiforgeryCookieFilter : IResultFilter
{
    public const string CookieName = "XSRF-TOKEN";
    public const string HeaderName = "X-XSRF-TOKEN";

    private readonly IAntiforgery antiforgery;

    public AntiforgeryCookieFilter(IAntiforgery antiforgery)
    {
        this.antiforgery = antiforgery;
    }

    public void OnResultExecuting(ResultExecutingContext context)
    {
        var httpContext = context.HttpContext;
        if (!HttpMethods.IsGet(httpContext.Request.Method))
        {
            return;
        }

        // Also sets the HttpOnly antiforgery cookie the request token is paired with.
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        httpContext.Response.Cookies.Append(CookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Strict,
            Secure = httpContext.Request.IsHttps,
            Path = "/",
        });
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
