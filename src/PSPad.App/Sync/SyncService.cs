using System.Text.Json;
using PSPad.Abstractions;
using PSPad.App.Api;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.Contracts;

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
        ["referenceitems"] = typeof(Module.Tasks.References.ReferenceItem),
        ["areaviews"] = typeof(Module.Presentation.AreaViews.AreaView),
        ["listviews"] = typeof(Module.Presentation.ListViews.ListView),
        ["budgets"] = typeof(Module.Money.Budgets.Budget),
        ["moneypreferences"] = typeof(Module.Money.Preferences.MoneyPreferences),
        ["moneyentries"] = typeof(Module.Money.Entries.MoneyEntry)
    };

    public async Task<SyncOutcome> SyncAsync(CancellationToken ct)
    {
        var (pushed, rejections, rejectedCommands) = await PushAsync();
        var pulled = await PullAsync(ct);

        return new SyncOutcome(pushed, pulled ?? 0, rejections, rejectedCommands, pulled is not null);
    }

    async Task<(int Pushed, IReadOnlyList<string> Rejections, IReadOnlyList<Guid> RejectedCommands)> PushAsync()
    {
        var batch = await outbox.PeekAsync(BatchSize);
        if (batch.Count == 0)
        {
            return (0, [], []);
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

        if (rejected?.Rejection is not { } reason)
        {
            return (accepted, [], []);
        }

        return (accepted, [reason], [batch[accepted].CommandId]);
    }

    public static string CollectionsFingerprint { get; } =
        string.Join(",", Collections.Keys.OrderBy(key => key, StringComparer.Ordinal));

    async Task<int?> PullAsync(CancellationToken ct)
    {
        var storedFingerprint = await replica.CollectionsFingerprintAsync();

        // A client built before a collection existed skipped its rows silently while still
        // advancing its marker past them, so a stale fingerprint forces one full re-pull.
        var since = storedFingerprint == CollectionsFingerprint ? await replica.MarkerAsync() : 0;
        var response = await api.SyncAsync(since, []);

        if (response is null)
        {
            return null;
        }

        var pulled = await SaveAsync(response.Documents);

        var me = await replica.OwnerAsync();

        // With no recorded owner, every list looks foreign -- skip reconciliation entirely so the
        // user's own lists are never purged.
        if (me is { } owner && response.MemberListIds is { } memberLists)
        {
            var missing = await ReconcileAsync(owner, memberLists);

            if (missing.Count > 0 && await FullPullAsync(response.Marker, missing) is { } whole)
            {
                pulled += await SaveAsync(whole.Documents);
            }

            await DropStrandedAsync(owner);
        }

        await replica.SetMarkerAsync(response.Marker);
        await replica.SetCollectionsFingerprintAsync(CollectionsFingerprint);
        return pulled;
    }

    // A failing full pull must not stall the marker -- the missing list stays missing and the
    // next sync retries it, instead of every delta pull wedging behind it forever.
    async Task<SyncResponse?> FullPullAsync(long marker, IReadOnlyCollection<Guid> missing)
    {
        try
        {
            return await api.SyncAsync(marker, missing);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    async Task<int> SaveAsync(IReadOnlyDictionary<string, JsonElement[]> documents)
    {
        var saved = 0;

        foreach (var (collection, rows) in documents)
        {
            if (!Collections.TryGetValue(collection, out var type))
            {
                continue;
            }

            foreach (var row in rows)
            {
                var aggregate = (Aggregate)JsonSerializer.Deserialize(row.GetRawText(), type,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                await replica.SaveAsync(aggregate);
                saved++;
            }
        }

        return saved;
    }

    async Task<IReadOnlyList<Guid>> ReconcileAsync(Guid me, IReadOnlyCollection<Guid> memberLists)
    {
        var lists = await replica.LoadAllAsync<Module.Tasks.Lists.TaskList>(me);
        var held = lists.Select(list => list.Id).ToHashSet();

        foreach (var lost in lists.Where(list => list.UserId != me && !memberLists.Contains(list.Id)))
        {
            await PurgeAsync(lost.Id);
        }

        return memberLists.Where(id => !held.Contains(id)).ToArray();
    }

    // A row the owner moved out of a shared list arrives once, naming a list this device never holds.
    async Task DropStrandedAsync(Guid me)
    {
        var held = (await replica.LoadAllAsync<Module.Tasks.Lists.TaskList>(me)).Select(list => list.Id).ToHashSet();

        foreach (var task in (await replica.LoadAllAsync<Module.Tasks.Tasks.TodoTask>(Guid.Empty))
                     .Where(task => task.UserId != me && !held.Contains(task.ListId)))
        {
            await replica.RemoveAsync(task.Id);
        }

        foreach (var item in (await replica.LoadAllAsync<Module.Tasks.References.ReferenceItem>(Guid.Empty))
                     .Where(item => item.UserId != me && !held.Contains(item.ListId)))
        {
            await replica.RemoveAsync(item.Id);
        }
    }

    async Task PurgeAsync(Guid listId)
    {
        foreach (var task in (await replica.LoadAllAsync<Module.Tasks.Tasks.TodoTask>(Guid.Empty))
                     .Where(task => task.ListId == listId))
        {
            await replica.RemoveAsync(task.Id);
        }

        foreach (var item in (await replica.LoadAllAsync<Module.Tasks.References.ReferenceItem>(Guid.Empty))
                     .Where(item => item.ListId == listId))
        {
            await replica.RemoveAsync(item.Id);
        }

        await replica.RemoveAsync(listId);
    }

    public async Task<JoinOutcome> JoinAsync(string token, string code, CancellationToken ct)
    {
        var outcome = await api.JoinAsync(token, code);
        if (outcome is JoinOutcome.Joined joined)
        {
            await SaveAsync(joined.Documents);
        }

        return outcome;
    }
}
