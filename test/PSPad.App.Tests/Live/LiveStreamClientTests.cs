using System.Net;
using PSPad.App.Live;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Live;

[UnitTest]
public class LiveStreamClientTests
{
    static readonly HttpRequestOptionsKey<bool> Streaming = new("WebAssemblyEnableStreamingResponse");

    [Fact]
    public async Task OpensTheStreamWithBrowserStreamingOn()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, "event: ready\ndata: ready\n\n");
        var client = new LiveStreamClient(new HttpClient(handler) { BaseAddress = new Uri("http://api/") });

        await using var stream = await client.OpenAsync(CancellationToken.None);

        Assert.True(handler.Last!.Options.TryGetValue(Streaming, out var on) && on);
        Assert.Equal("event: ready\ndata: ready\n\n", await new StreamReader(stream).ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ARefusedStreamThrowsWithItsStatus()
    {
        var client = new LiveStreamClient(new HttpClient(new StubHttpHandler(HttpStatusCode.TooManyRequests))
        {
            BaseAddress = new Uri("http://api/")
        });

        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => client.OpenAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
    }
}
