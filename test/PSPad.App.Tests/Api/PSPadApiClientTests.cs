using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PSPad.App.Api;
using PSPad.App.Sync;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Api;

[UnitTest]
public class PSPadApiClientTests
{
    [Fact]
    public async Task ItPutsTheTimeZoneAndReturnsTheUpdatedUser()
    {
        var handler = new StubHandler(new MeResponse(
            Guid.NewGuid(), "Ada Lovelace", "ada@example.com", "Europe/Warsaw"));
        var client = new PSPadApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        });

        var me = await client.SetTimeZoneAsync("Europe/Warsaw");

        Assert.Equal("Europe/Warsaw", me!.TimeZone);
        Assert.Equal(HttpMethod.Put, handler.LastRequest!.Method);
        Assert.EndsWith("api/me/timezone", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task ItReturnsNullWhenTheServerRejectsTheZone()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest);
        var client = new PSPadApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        });

        Assert.Null(await client.SetTimeZoneAsync("Mars/Olympus_Mons"));
    }

    [Fact]
    public async Task DeleteAccountAsyncReturnsTheParsedResponseOnSuccess()
    {
        var client = new PSPadApiClient(new HttpClient(new DeleteAccountHandler(HttpStatusCode.OK, """{"keycloakRemoved":true}"""))
        {
            BaseAddress = new Uri("http://localhost")
        });

        var result = await client.DeleteAccountAsync();

        Assert.NotNull(result);
        Assert.True(result!.KeycloakRemoved);
    }

    [Fact]
    public async Task DeleteAccountAsyncReturnsNullOnFailureStatus()
    {
        var client = new PSPadApiClient(new HttpClient(new DeleteAccountHandler(HttpStatusCode.InternalServerError, ""))
        {
            BaseAddress = new Uri("http://localhost")
        });

        Assert.Null(await client.DeleteAccountAsync());
    }

    [Fact]
    public async Task DeleteAccountAsyncReturnsNullWhenOffline()
    {
        var client = new PSPadApiClient(new HttpClient(new ThrowingHandler())
        {
            BaseAddress = new Uri("http://localhost")
        });

        Assert.Null(await client.DeleteAccountAsync());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, typeof(JoinOutcome.Invalid))]
    [InlineData(HttpStatusCode.Gone, typeof(JoinOutcome.Expired))]
    [InlineData(HttpStatusCode.TooManyRequests, typeof(JoinOutcome.TooManyTries))]
    public async Task JoinAsyncMapsAFailedStatusToItsOutcome(HttpStatusCode status, Type outcome)
    {
        var client = new PSPadApiClient(new HttpClient(new DeleteAccountHandler(status, ""))
        {
            BaseAddress = new Uri("http://localhost")
        });

        Assert.IsType(outcome, await client.JoinAsync("token", "K7M4PX"));
    }

    [Fact]
    public async Task JoinAsyncPostsTheCodeAndReturnsTheJoinedList()
    {
        var listId = Guid.NewGuid();
        var handler = new JoinHandler(new JoinListResponse(listId, new Dictionary<string, JsonElement[]>()));
        var client = new PSPadApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        });

        var outcome = await client.JoinAsync("token", "K7M4PX");

        Assert.Equal(listId, Assert.IsType<JoinOutcome.Joined>(outcome).ListId);
        Assert.Equal(new JoinListRequest("token", "K7M4PX"), handler.Request);
    }

    sealed class JoinHandler(JoinListResponse response) : HttpMessageHandler
    {
        public JoinListRequest? Request { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = await request.Content!.ReadFromJsonAsync<JoinListRequest>(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(response) };
        }
    }

    sealed class StubHandler : HttpMessageHandler
    {
        readonly MeResponse? _response;
        readonly HttpStatusCode _statusCode;

        public HttpRequestMessage? LastRequest { get; private set; }

        internal StubHandler(MeResponse response)
        {
            _response = response;
            _statusCode = HttpStatusCode.OK;
        }

        internal StubHandler(HttpStatusCode statusCode)
        {
            _response = null;
            _statusCode = statusCode;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;

            if (_statusCode == HttpStatusCode.OK && _response != null)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(_response)
                };
            }

            return new HttpResponseMessage(_statusCode);
        }
    }

    sealed class DeleteAccountHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("offline");
    }
}
