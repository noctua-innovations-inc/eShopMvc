using System.Diagnostics.CodeAnalysis;
using eShopMvc.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Session;

namespace eShopMvc.Web.UnitTests.Infrastructure;

public class SessionStartFilterTests
{
    [Fact]
    public void OnActionExecuting_NewSessionOnViewController_StoresMachineNameAndLegacyFormattedStartTime()
    {
        var session = new TestSession();
        var filter = new SessionStartFilter(new FixedTimeProvider(new DateTime(2026, 10, 18, 17, 5, 9)));

        filter.OnActionExecuting(Context(new ViewController(), session));

        Assert.Equal(Environment.MachineName, session.GetString(SessionStartFilter.MachineNameKey));
        Assert.Equal("10/18/2026 5:05:09 PM", session.GetString(SessionStartFilter.SessionStartTimeKey));
    }

    [Fact]
    public void OnActionExecuting_ExistingSession_KeepsTheOriginalStartTime()
    {
        var session = new TestSession();
        var time = new FixedTimeProvider(new DateTime(2026, 10, 18, 9, 0, 0));
        var filter = new SessionStartFilter(time);
        filter.OnActionExecuting(Context(new ViewController(), session));

        time.Now = new DateTime(2026, 10, 18, 10, 30, 0);
        filter.OnActionExecuting(Context(new ViewController(), session));

        Assert.Equal("10/18/2026 9:00:00 AM", session.GetString(SessionStartFilter.SessionStartTimeKey));
    }

    [Fact]
    public void OnActionExecuting_ApiController_DoesNotTouchTheSession()
    {
        var session = new TestSession();
        var filter = new SessionStartFilter(new FixedTimeProvider(DateTime.Now));

        filter.OnActionExecuting(Context(new ApiController(), session));

        Assert.Empty(session.Keys);
    }

    [Theory]
    [InlineData(0, "10/18/2026 12:00:00 AM")]
    [InlineData(12, "10/18/2026 12:00:00 PM")]
    [InlineData(23, "10/18/2026 11:00:00 PM")]
    public void FormatStartTime_UsesLegacyEnUsPatternWithAPlainSpace(int hour, string expected)
    {
        var text = SessionStartFilter.FormatStartTime(new DateTime(2026, 10, 18, hour, 0, 0));

        Assert.Equal(expected, text);
        Assert.DoesNotContain(' ', text); // ICU's narrow no-break space before AM/PM
    }

    private static ActionExecutingContext Context(object controller, ISession session)
    {
        var http = new DefaultHttpContext();
        http.Features.Set<ISessionFeature>(new SessionFeature { Session = session });
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(action, [], new Dictionary<string, object?>(), controller);
    }

    private sealed class ViewController : Controller;

    private sealed class ApiController : ControllerBase;

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(Now, DateTimeKind.Utc));
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = [];

        public bool IsAvailable => true;
        public string Id => "test";
        public IEnumerable<string> Keys => values.Keys;

        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => values.TryGetValue(key, out value);
    }
}
