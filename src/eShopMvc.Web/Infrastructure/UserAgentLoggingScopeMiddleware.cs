using System.Collections;

namespace eShopMvc.Web.Infrastructure;

/// <summary>
/// Adds the request's User-Agent to the logging scope of everything logged during the request (Decision L2;
/// legacy log4net "requestinfo"). ASP.NET Core already adds TraceId, SpanId, RequestId and RequestPath.
/// The query string is deliberately not logged.
/// </summary>
public sealed class UserAgentLoggingScopeMiddleware(RequestDelegate next, ILogger<UserAgentLoggingScopeMiddleware> logger)
{
    public const string ScopeKey = "UserAgent";

    public async Task InvokeAsync(HttpContext context)
    {
        var userAgent = context.Request.Headers.UserAgent.ToString();
        if (userAgent.Length == 0)
        {
            await next(context);
            return;
        }

        // Scopes are shared by all loggers (the logger factory's scope provider), so one scope covers the request.
        using (logger.BeginScope(new UserAgentScope(userAgent)))
        {
            await next(context);
        }
    }

    /// <summary>
    /// A key/value list for structured formatters (JSON: <c>"UserAgent": "…"</c>) and a readable
    /// <see cref="ToString"/> for the simple formatter (<c>UserAgent:…</c>), like the built-in request scopes.
    /// </summary>
    public sealed class UserAgentScope(string userAgent) : IReadOnlyList<KeyValuePair<string, object?>>
    {
        public int Count => 1;

        public KeyValuePair<string, object?> this[int index] =>
            index == 0 ? new(ScopeKey, userAgent) : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            yield return this[0];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => $"{ScopeKey}:{userAgent}";
    }
}
