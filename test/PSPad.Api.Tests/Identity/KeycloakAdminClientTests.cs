using System.Net;
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

    static KeycloakAdminClient Client(HttpMessageHandler handler) =>
        new(new HttpClient(handler), Options.Create(new KeycloakAdminOptions
        {
            Authority = "http://localhost:8080/realms/psplace",
            AdminUser = "admin",
            AdminPassword = "admin"
        }));

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
