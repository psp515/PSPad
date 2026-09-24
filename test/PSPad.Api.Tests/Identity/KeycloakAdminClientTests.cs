using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PSPad.Api.Identity;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Identity;

[UnitTest]
public class KeycloakAdminClientTests
{
    [Fact]
    public async Task ItDeletesTheUserAfterFetchingAnAdminToken()
    {
        var handler = new ScriptedHandler(
            Respond(HttpStatusCode.OK, """{"access_token":"admin-token"}"""),
            Respond(HttpStatusCode.NoContent, ""));

        var result = await Client(handler).DeleteUserAsync("kc-subject", CancellationToken.None);

        Assert.True(result);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(
            "http://localhost:8080/realms/master/protocol/openid-connect/token",
            handler.Requests[0].RequestUri!.ToString());
        Assert.Equal(
            "http://localhost:8080/admin/realms/psplace/users/kc-subject",
            handler.Requests[1].RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Requests[1].Headers.Authorization!.Scheme);
        Assert.Equal("admin-token", handler.Requests[1].Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task ANotFoundOnDeleteCountsAsSuccessBecauseTheUserIsAlreadyGone()
    {
        var handler = new ScriptedHandler(
            Respond(HttpStatusCode.OK, """{"access_token":"admin-token"}"""),
            Respond(HttpStatusCode.NotFound, ""));

        Assert.True(await Client(handler).DeleteUserAsync("kc-subject", CancellationToken.None));
    }

    [Fact]
    public async Task ItRetriesUpToThreeTimesBeforeGivingUp()
    {
        var handler = new ScriptedHandler(
            Respond(HttpStatusCode.OK, """{"access_token":"admin-token"}"""),
            Respond(HttpStatusCode.InternalServerError, ""),
            Respond(HttpStatusCode.OK, """{"access_token":"admin-token"}"""),
            Respond(HttpStatusCode.InternalServerError, ""),
            Respond(HttpStatusCode.OK, """{"access_token":"admin-token"}"""),
            Respond(HttpStatusCode.InternalServerError, ""));

        var result = await Client(handler).DeleteUserAsync("kc-subject", CancellationToken.None);

        Assert.False(result);
        Assert.Equal(6, handler.Requests.Count);
    }

    [Fact]
    public async Task ItSucceedsOnASubsequentAttemptAfterATransientFailure()
    {
        var handler = new ScriptedHandler(
            Respond(HttpStatusCode.OK, """{"access_token":"admin-token"}"""),
            Respond(HttpStatusCode.InternalServerError, ""),
            Respond(HttpStatusCode.OK, """{"access_token":"admin-token"}"""),
            Respond(HttpStatusCode.NoContent, ""));

        Assert.True(await Client(handler).DeleteUserAsync("kc-subject", CancellationToken.None));
    }

    [Fact]
    public async Task AFailedTokenFetchFailsTheAttemptWithoutCallingDelete()
    {
        var handler = new ScriptedHandler(
            Respond(HttpStatusCode.Unauthorized, ""),
            Respond(HttpStatusCode.Unauthorized, ""),
            Respond(HttpStatusCode.Unauthorized, ""));

        var result = await Client(handler).DeleteUserAsync("kc-subject", CancellationToken.None);

        Assert.False(result);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task ExhaustingEveryRetryLogsAWarningSoAnAdministratorHasSomethingToNotice()
    {
        var handler = new ScriptedHandler(
            Respond(HttpStatusCode.OK, """{"access_token":"admin-token"}"""),
            Respond(HttpStatusCode.InternalServerError, ""),
            Respond(HttpStatusCode.OK, """{"access_token":"admin-token"}"""),
            Respond(HttpStatusCode.InternalServerError, ""),
            Respond(HttpStatusCode.OK, """{"access_token":"admin-token"}"""),
            Respond(HttpStatusCode.InternalServerError, ""));
        var logger = new RecordingLogger<KeycloakAdminClient>();

        var result = await Client(handler, logger).DeleteUserAsync("kc-subject", CancellationToken.None);

        Assert.False(result);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task AFailedTokenFetchLogsAWarning()
    {
        var handler = new ScriptedHandler(
            Respond(HttpStatusCode.Unauthorized, ""),
            Respond(HttpStatusCode.Unauthorized, ""),
            Respond(HttpStatusCode.Unauthorized, ""));
        var logger = new RecordingLogger<KeycloakAdminClient>();

        await Client(handler, logger).DeleteUserAsync("kc-subject", CancellationToken.None);

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    static KeycloakAdminClient Client(HttpMessageHandler handler, ILogger<KeycloakAdminClient>? logger = null) =>
        new(new HttpClient(handler), Options.Create(new KeycloakAdminOptions
        {
            Authority = "http://localhost:8080/realms/psplace",
            AdminUser = "admin",
            AdminPassword = "admin"
        }), logger ?? new RecordingLogger<KeycloakAdminClient>());

    sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    static (HttpStatusCode Status, string Body) Respond(HttpStatusCode status, string body) => (status, body);

    sealed class ScriptedHandler(params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
    {
        int _next;

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var (status, body) = responses[_next];
            _next++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
