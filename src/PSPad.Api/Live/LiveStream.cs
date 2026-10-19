using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using PSPad.Abstractions;
using PSPad.Api.Identity;

namespace PSPad.Api.Live;

public static class LiveStream
{
    public static async Task RunAsync(
        HttpContext http,
        ICurrentUser user,
        LiveConnections connections,
        IOptions<LiveOptions> options,
        IClock clock,
        IHostApplicationLifetime lifetime)
    {
        using var subscription = connections.Open(user.UserId);
        if (subscription is null)
        {
            http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return;
        }

        var response = http.Response;
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache, no-transform";
        response.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var openedAt = clock.UtcNow;
        var life = LiveStreamEnd.For(http.User, openedAt, options.Value.MaxStream) - openedAt;
        using var end = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted, lifetime.ApplicationStopping);
        end.CancelAfter(life > TimeSpan.Zero ? life : TimeSpan.Zero);

        try
        {
            await WriteAsync(response, SseFrames.Ready, end.Token);
            await PumpAsync(response, subscription, options.Value.Heartbeat, end.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    static async Task PumpAsync(
        HttpResponse response, LiveSubscription subscription, TimeSpan heartbeat, CancellationToken ct)
    {
        Task<bool>? nudged = null;

        while (true)
        {
            nudged ??= subscription.Nudges.WaitToReadAsync(ct).AsTask();
            using var quiet = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var beat = Task.Delay(heartbeat, quiet.Token);

            if (await Task.WhenAny(nudged, beat) == nudged)
            {
                await quiet.CancelAsync();
                if (!await nudged)
                {
                    return;
                }

                subscription.Nudges.TryRead(out _);
                nudged = null;
                await WriteAsync(response, SseFrames.Sync, ct);
            }
            else
            {
                await beat;
                await WriteAsync(response, SseFrames.Heartbeat, ct);
            }
        }
    }

    static async Task WriteAsync(HttpResponse response, byte[] frame, CancellationToken ct)
    {
        await response.Body.WriteAsync(frame, ct);
        await response.Body.FlushAsync(ct);
    }
}
