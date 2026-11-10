using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace eShopMvc.Web.Infrastructure;

/// <summary>
/// Replaces legacy <c>Session_Start</c> (Decision R-11): on the first MVC request of a session, stores the
/// machine name and the session start time, which the layout footer prints as "{MachineName}, {SessionStartTime}".
/// Applies only to <see cref="Controller"/>-based (view) controllers; API controllers get no session, as legacy
/// Web API had none. Exception (Decision P2-8): <c>GET /api/v1/session</c> starts it for the React footer through
/// <see cref="EnsureStarted"/>.
/// </summary>
public sealed class SessionStartFilter(TimeProvider timeProvider) : IActionFilter
{
    public const string MachineNameKey = "MachineName";
    public const string SessionStartTimeKey = "SessionStartTime";

    // Legacy printed DateTime.Now with en-US "G" on .NET Framework: "10/18/2026 5:20:20 PM". .NET 10 takes
    // "G" from ICU, which may put U+202F before AM/PM, so the pattern is spelled out with a plain space.
    public const string SessionStartTimeFormat = "M/d/yyyy h:mm:ss tt";

    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.Controller is not Controller)
        {
            return;
        }

        EnsureStarted(context.HttpContext.Session, timeProvider);
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    /// <summary>Stores the machine name and start time unless this session already has them.</summary>
    public static void EnsureStarted(ISession session, TimeProvider timeProvider)
    {
        if (session.GetString(MachineNameKey) is null)
        {
            session.SetString(MachineNameKey, Environment.MachineName);
            session.SetString(SessionStartTimeKey, FormatStartTime(timeProvider.GetLocalNow().DateTime));
        }
    }

    public static string FormatStartTime(DateTime localTime) => localTime.ToString(SessionStartTimeFormat, EnUs);
}
