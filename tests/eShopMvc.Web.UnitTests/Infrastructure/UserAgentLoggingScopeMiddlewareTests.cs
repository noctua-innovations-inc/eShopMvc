using eShopMvc.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace eShopMvc.Web.UnitTests.Infrastructure;

public class UserAgentLoggingScopeMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithUserAgent_RunsTheRestOfThePipelineInsideAUserAgentScope()
    {
        var logger = new ScopeRecordingLogger();
        var scopeOpenDuringNext = false;
        var middleware = new UserAgentLoggingScopeMiddleware(_ =>
        {
            scopeOpenDuringNext = logger.OpenScopes == 1;
            return Task.CompletedTask;
        }, logger);
        var context = new DefaultHttpContext();
        context.Request.Headers.UserAgent = "Mozilla/5.0 (test)";

        await middleware.InvokeAsync(context);

        var state = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(Assert.Single(logger.Scopes));
        Assert.Equal([new("UserAgent", "Mozilla/5.0 (test)")], state); // structured (JSON formatter)
        Assert.Equal("UserAgent:Mozilla/5.0 (test)", state.ToString()); // readable (simple formatter)
        Assert.True(scopeOpenDuringNext);
        Assert.Equal(0, logger.OpenScopes);
    }

    [Fact]
    public async Task InvokeAsync_WithoutUserAgent_OpensNoScope()
    {
        var logger = new ScopeRecordingLogger();
        var called = false;
        var middleware = new UserAgentLoggingScopeMiddleware(_ => { called = true; return Task.CompletedTask; }, logger);

        await middleware.InvokeAsync(new DefaultHttpContext());

        Assert.True(called);
        Assert.Empty(logger.Scopes);
    }

    private sealed class ScopeRecordingLogger : ILogger<UserAgentLoggingScopeMiddleware>
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
