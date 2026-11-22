using PSPad.App.Api;
using PSPad.App.State;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Api;

[UnitTest]
public class TimeZoneHintHandlerTests
{
    [Fact]
    public async Task EveryApiCallNamesTheBrowsersZone()
    {
        var inner = new CapturingHandler();
        using var client = new HttpClient(new TimeZoneHintHandler(new BrowserTimeZone("Europe/Warsaw")) { InnerHandler = inner });

        await client.GetAsync("https://api.test/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(["Europe/Warsaw"], inner.Request!.Headers.GetValues(TimeZoneHint.Header));
    }

    sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
