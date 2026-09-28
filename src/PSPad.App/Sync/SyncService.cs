using System.Text.Json;
using PSPad.Abstractions;
using PSPad.App.Api;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;

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
        ["inboxes"] = typeof(Module.Tasks.Inbox.Inbox),
        ["referenceitems"] = typeof(Module.Tasks.References.ReferenceItem)
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

        var rejected = accepted < responses.Count ? responses[accepted] : null;

        // A structural rejection (unknown command, malformed payload, wrong user, no handler) is a
        // verdict against the command's own contents -- resending it unchanged rejects it
        // identically forever, so it is dropped once surfaced instead of blocking everything behind
        // it. A domain rejection from the command's own handler stays queued, unchanged from before.
        var removeThrough = rejected is { Unrecoverable: true } ? accepted : accepted - 1;
        if (removeThrough >= 0)
        {
            await outbox.RemoveThroughAsync(batch[removeThrough].Position);
        }

        var rejections = rejected?.Rejection is { } reason ? new[] { reason } : Array.Empty<string>();
        return (accepted, rejections);
    }

    public static string CollectionsFingerprint { get; } =
        string.Join(",", Collections.Keys.OrderBy(key => key, StringComparer.Ordinal));

    async Task<int> PullAsync(CancellationToken ct)
    {
        var storedFingerprint = await replica.CollectionsFingerprintAsync();

        // A client built before a collection existed skipped its rows silently while still
        // advancing its marker past them, so a stale fingerprint forces one full re-pull.
        var since = storedFingerprint == CollectionsFingerprint ? await replica.MarkerAsync() : 0;
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
        await replica.SetCollectionsFingerprintAsync(CollectionsFingerprint);
        return pulled;
    }
}
