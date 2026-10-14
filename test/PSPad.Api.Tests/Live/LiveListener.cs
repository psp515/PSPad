using System.Threading.Channels;
using PSPad.App.Live;

namespace PSPad.Api.Tests.Live;

public sealed class LiveListener : IAsyncDisposable
{
    readonly Channel<string> _frames = Channel.CreateUnbounded<string>();
    readonly CancellationTokenSource _stop = new();
    readonly Task _pump;

    LiveListener(HttpResponseMessage response)
    {
        Response = response;
        _pump = PumpAsync();
    }

    public HttpResponseMessage Response { get; }

    public static async Task<LiveListener> OpenAsync(HttpClient client, CancellationToken ct, string path = "/api/live")
    {
        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, path), HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var listener = new LiveListener(response);
        Assert.Equal("ready", await listener.NextAsync(TimeSpan.FromSeconds(10)));
        return listener;
    }

    public async Task<string?> NextAsync(TimeSpan within, bool skipHeartbeats = true)
    {
        using var timeout = new CancellationTokenSource(within);

        while (await _frames.Reader.WaitToReadAsync(timeout.Token))
        {
            if (_frames.Reader.TryRead(out var frame) && !(skipHeartbeats && frame == SseReader.Heartbeat))
            {
                return frame;
            }
        }

        return null;
    }

    public async Task<bool> StaysQuietAsync(TimeSpan window)
    {
        try
        {
            return await NextAsync(window) is null;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }

    public async Task<bool> EndsWithinAsync(TimeSpan within)
    {
        try
        {
            using var timeout = new CancellationTokenSource(within);
            while (await _frames.Reader.WaitToReadAsync(timeout.Token))
            {
                _frames.Reader.TryRead(out _);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    async Task PumpAsync()
    {
        try
        {
            await using var stream = await Response.Content.ReadAsStreamAsync(_stop.Token);
            await foreach (var frame in SseReader.FramesAsync(stream, _stop.Token))
            {
                await _frames.Writer.WriteAsync(frame, _stop.Token);
            }

            _frames.Writer.TryComplete();
        }
        catch (Exception exception)
        {
            _frames.Writer.TryComplete(exception is OperationCanceledException ? null : exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _pump;
        Response.Dispose();
        _stop.Dispose();
    }
}
