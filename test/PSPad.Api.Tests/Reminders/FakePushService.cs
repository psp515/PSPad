using System.Collections.Concurrent;
using System.Net;

namespace PSPad.Api.Tests.Reminders;

public sealed class FakePushService : HttpMessageHandler
{
    readonly ConcurrentQueue<HttpStatusCode> _answers = new();
    readonly ConcurrentQueue<PushRequest> _requests = new();

    public IReadOnlyList<PushRequest> Requests => [.. _requests];

    public IReadOnlyList<PushRequest> To(BrowserDevice device) =>
        [.. _requests.Where(request => request.Uri.ToString() == device.Endpoint)];

    public void Answer(params HttpStatusCode[] statuses)
    {
        foreach (var status in statuses)
        {
            _answers.Enqueue(status);
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in request.Headers)
        {
            headers[name] = string.Join(", ", values);
        }

        var body = Array.Empty<byte>();
        if (request.Content is not null)
        {
            foreach (var (name, values) in request.Content.Headers)
            {
                headers[name] = string.Join(", ", values);
            }

            body = await request.Content.ReadAsByteArrayAsync(ct);
        }

        _requests.Enqueue(new PushRequest(request.RequestUri!, headers, body));
        return new HttpResponseMessage(_answers.TryDequeue(out var status) ? status : HttpStatusCode.Created);
    }
}
