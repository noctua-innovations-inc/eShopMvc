using eShopMvc.Web.Infrastructure;
using eShopMvc.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace eShopMvc.Web.Controllers.WebApi;

// Decision P2-8: the React footer's session values. The page shell is a static file, so SessionStartFilter
// never sees a page request; this is the one API endpoint that starts a session (R-11). Same session cookie
// as the Razor pages, so both show the same values.
[ApiController]
[Route("api/v1/session")]
public class SessionApiController : ControllerBase
{
    private readonly TimeProvider timeProvider;

    public SessionApiController(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    // GET api/v1/session
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public SessionInfo Get()
    {
        var session = HttpContext.Session;
        SessionStartFilter.EnsureStarted(session, timeProvider);
        return new SessionInfo(
            session.GetString(SessionStartFilter.MachineNameKey)!,
            session.GetString(SessionStartFilter.SessionStartTimeKey)!);
    }
}
