using eShopMvc.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace eShopMvc.Web.UnitTests.Infrastructure;

// Decision P2-19.
public class ClientPageLoggingScopeMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithPageHeader_RunsTheRestOfThePipelineInsideAPageScope()
    {
        var logger = new ScopeRecordingLogger();
        var scopeOpenDuringNext = false;
        var middleware = new ClientPageLoggingScopeMiddleware(_ =>
        {
            scopeOpenDuringNext = logger.OpenScopes == 1;
            return Task.CompletedTask;
        }, logger);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Client-Page"] = "/Catalog/Edit/5";

        await middleware.InvokeAsync(context);

        var state = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(Assert.Single(logger.Scopes));
        Assert.Equal([new("Page", "/Catalog/Edit/5")], state); // structured (JSON formatter)
        Assert.Equal("Page:/Catalog/Edit/5", state.ToString()); // readable (simple formatter)
        Assert.True(scopeOpenDuringNext);
        Assert.Equal(0, logger.OpenScopes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Catalog/Edit/5")] // not a path
    [InlineData("//evil.example/x")]
    [InlineData("https://evil.example/")]
    [InlineData("/Catalog?pageSize=5")] // the query string is not logged (L2)
    [InlineData("/Catalog#top")]
    [InlineData("/Catalog/Edit/5 forged=true")]
    [InlineData("/Catalog/Edit/5\nNow loading... forged line")]
    [InlineData("/Catalog/\"Edit\"")]
    [InlineData("/Catalog\\Edit")]
    [InlineData("/Catalog/Édit")] // a browser sends this percent-encoded
    public async Task InvokeAsync_WithoutAPlainPath_OpensNoScope(string? header)
    {
        var logger = new ScopeRecordingLogger();
        var called = false;
        var middleware = new ClientPageLoggingScopeMiddleware(_ => { called = true; return Task.CompletedTask; }, logger);
        var context = new DefaultHttpContext();
        if (header != null)
        {
            context.Request.Headers["X-Client-Page"] = header;
        }

        await middleware.InvokeAsync(context);

        Assert.True(called);
        Assert.Empty(logger.Scopes);
    }

    [Fact]
    public async Task InvokeAsync_WithTwoHeaderValues_OpensNoScope()
    {
        var logger = new ScopeRecordingLogger();
        var middleware = new ClientPageLoggingScopeMiddleware(_ => Task.CompletedTask, logger);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Client-Page"] = new StringValues(["/Catalog", "/Catalog/Create"]);

        await middleware.InvokeAsync(context);

        Assert.Empty(logger.Scopes);
    }

    [Theory]
    [InlineData("/", true)]
    [InlineData("/Catalog/Details/abc%20def", true)]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void IsPagePath_AcceptsShortPlainPaths(object pathOrLength, bool expected)
    {
        var path = pathOrLength is int length ? "/" + new string('a', length - 1) : (string)pathOrLength;

        Assert.Equal(expected, ClientPageLoggingScopeMiddleware.IsPagePath(path));
    }

    private sealed class ScopeRecordingLogger : ILogger<ClientPageLoggingScopeMiddleware>
    {
        public List<object> Scopes { get; } = [];
        public int OpenScopes { get; private set; }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            Scopes.Add(state);
            OpenScopes++;
            return new Scope(() => OpenScopes--);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }

        private sealed class Scope(Action dispose) : IDisposable
        {
            public void Dispose() => dispose();
        }
    }
}
