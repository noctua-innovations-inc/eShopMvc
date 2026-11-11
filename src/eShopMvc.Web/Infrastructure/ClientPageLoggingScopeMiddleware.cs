using System.Collections;

namespace eShopMvc.Web.Infrastructure;

/// <summary>
/// Decision P2-19: the React client names the page it is showing in the <see cref="HeaderName"/> header of
/// its API calls (the page URL's path, e.g. <c>/Catalog/Edit/5</c>). It goes into the logging scope of
/// everything logged during the request, so the API's "Now loading..." lines say which page they were for,
/// as the Razor actions' own lines did. The value comes from the client: anything that isn't a short,
/// plain path is ignored.
/// </summary>
public sealed class ClientPageLoggingScopeMiddleware(RequestDelegate next, ILogger<ClientPageLoggingScopeMiddleware> logger)
{
    public const string HeaderName = "X-Client-Page";
    public const string ScopeKey = "Page";
    public const int MaxLength = 200;

    public async Task InvokeAsync(HttpContext context)
    {
        var values = context.Request.Headers[HeaderName];
        if (values.Count != 1 || !IsPagePath(values[0]))
        {
            await next(context);
            return;
        }

        using (logger.BeginScope(new PageScope(values[0]!)))
        {
            await next(context);
        }
    }

    /// <summary>
    /// A path as <c>location.pathname</c> gives it: starts with one slash, printable ASCII only (anything
    /// else is percent-encoded there), no query string, at most <see cref="MaxLength"/> characters.
    /// </summary>
    public static bool IsPagePath(string? value) =>
        value is { Length: > 0 and <= MaxLength }
        && value[0] == '/'
        && !value.StartsWith("//", StringComparison.Ordinal)
        && value.All(c => c is > ' ' and <= '~' and not ('?' or '#' or '\\' or '"'));

    /// <summary>
    /// A key/value list for structured formatters (JSON: <c>"Page": "…"</c>) and a readable
    /// <see cref="ToString"/> for the simple formatter (<c>Page:…</c>), like the built-in request scopes.
    /// </summary>
    public sealed class PageScope(string page) : IReadOnlyList<KeyValuePair<string, object?>>
    {
        public int Count => 1;

        public KeyValuePair<string, object?> this[int index] =>
            index == 0 ? new(ScopeKey, page) : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            yield return this[0];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => $"{ScopeKey}:{page}";
    }
}
