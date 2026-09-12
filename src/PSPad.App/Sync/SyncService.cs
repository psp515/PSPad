using System.Text.Json;
using PSPad.Abstractions;
using PSPad.App.Api;
using PSPad.App.State;

namespace PSPad.App.Sync;

public sealed class SyncService(ISyncApi api, IReplica replica, IOutbox outbox)
{
    const int BatchSize = 50;

    // "users" is deliberately absent: the client reads its own user from /api/me, and User
    // lives in a module PSPad.App does not reference.
    static readonly Dictionary<string, Type> Collections = new()
    {
        ["areas"] = typeof(Module.Tasks.Areas.Area),
        ["tasklists"] = typeof(Module.Tasks.Lists.TaskList),
        ["todotasks"] = typeof(Module.Tasks.Tasks.TodoTask),
        ["goals"] = typeof(Module.Tasks.Goals.Goal),
        ["inboxes"] = typeof(Module.Tasks.Inbox.Inbox)
    };

    public async Task<SyncOutcome> SyncAsync(CancellationToken ct)
    {
        var (pushed, rejections) = await PushAsync();
        var pulled = await PullAsync(ct);

        return new SyncOutcome(pushed, pulled, rejections);
    }

    async Task<(int Pushed, IReadOnlyList<string> Rejections)> PushAsync()
    {
        var batch = await outbox.PeekAsync(BatchSize);
        if (batch.Count == 0)
        {
            return (0, []);
        }

        var responses = await api.SendAsync(batch.Select(entry => entry.Envelope).ToArray());
        var accepted = 0;

        while (accepted < responses.Count && responses[accepted].Accepted)
        {
            accepted++;
        }

        if (accepted > 0)
        {
            // Stop at the first rejection and keep everything after it -- a later command
            // usually depends on the one that failed, and shipping it anyway would leave the
            // client and server disagreeing about state.
            await outbox.RemoveThroughAsync(batch[accepted - 1].Position);
        }

        var rejections = accepted < responses.Count && responses[accepted].Rejection is { } reason
            ? new[] { reason }
            : Array.Empty<string>();

        return (accepted, rejections);
    }

    async Task<int> PullAsync(CancellationToken ct)
    {
        var since = await replica.MarkerAsync();
        var response = await api.SyncAsync(since);

        if (response is null)
        {
            return 0;
        }

        var pulled = 0;

        foreach (var (collection, documents) in response.Documents)
        {
            if (!Collections.TryGetValue(collection, out var type))
            {
                continue;
            }

            foreach (var document in documents)
            {
                var aggregate = (Aggregate)JsonSerializer.Deserialize(document.GetRawText(), type,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                await replica.SaveAsync(aggregate);
                pulled++;
            }
        }

        await replica.SetMarkerAsync(response.Marker);
        return pulled;
    }
}
