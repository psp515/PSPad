using System.Text.Json;
using PSPad.Abstractions;
using PSPad.App.Api;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.Contracts;

namespace PSPad.App.Sync;

public sealed class SyncService(ISyncApi api, IReplica replica, IOutbox outbox)
{
    public const int BatchBytes = 512 * 1024;

    static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

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
        ["moneyentries"] = typeof(Module.Money.Entries.MoneyEntry),
        ["balancesnapshots"] = typeof(Module.Money.Balance.BalanceSnapshot)
    };

    public async Task<SyncOutcome> SyncAsync(CancellationToken ct)
    {
        var (pushed, rejections, rejectedCommands) = await PushAsync();
        var pulled = await PullAsync(ct);

        return new SyncOutcome(pushed, pulled ?? 0, rejections, rejectedCommands, pulled is not null);
    }

    async Task<(int Pushed, IReadOnlyList<string> Rejections, IReadOnlyList<Guid> RejectedCommands)> PushAsync()
    {
        var batch = WithinBudget(await outbox.PeekAsync(CommandBatch.MaxEnvelopes));
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

    static IReadOnlyList<OutboxEntry> WithinBudget(IReadOnlyList<OutboxEntry> peeked)
    {
        var bytes = 0;
        var take = 0;

        while (take < peeked.Count)
        {
            bytes += JsonSerializer.SerializeToUtf8Bytes(peeked[take].Envelope, Wire).Length;
            if (take > 0 && bytes > BatchBytes)
            {
                break;
            }

            take++;
        }

        return take == peeked.Count ? peeked : peeked.Take(take).ToArray();
    }

    readonly HashSet<Guid> _freshJoins = [];

    public static string CollectionsFingerprint { get; } =
        string.Join(",", Collections.Keys.OrderBy(key => key, StringComparer.Ordinal));

    async Task<int?> PullAsync(CancellationToken ct)
    {
        var storedFingerprint = await replica.CollectionsFingerprintAsync();

        // A client built before a collection existed skipped its rows silently while still
        // advancing its marker past them, so a stale fingerprint forces one full re-pull.
        var since = storedFingerprint == CollectionsFingerprint ? await replica.MarkerAsync() : 0;
        var pulled = 0;
        var settledJoins = _freshJoins.ToArray();
        var me = await replica.OwnerAsync();
        var knownContainers = since == 0 ? null : await KnownContainersAsync(me);
        SyncResponse? response;

        while (true)
        {
            response = await api.SyncAsync(since, []);

            if (response is null)
            {
                return null;
            }

            pulled += await SaveAsync(response.Documents);

            if (!response.HasMore || response.Marker <= since)
            {
                break;
            }

            await replica.SetMarkerAsync(response.Marker);
            await replica.SetCollectionsFingerprintAsync(CollectionsFingerprint);
            since = response.Marker;
        }

        // With no recorded owner, every list and budget looks foreign -- skip reconciliation entirely so
        // the user's own are never purged.
        if (me is { } owner && (response.MemberListIds is not null || response.MemberBudgetIds is not null))
        {
            var joinedMidPull = _freshJoins.Except(settledJoins).ToHashSet();
            var missing = new List<Guid>();

            if (response.MemberListIds is { } memberLists)
            {
                await ReconcileAsync(owner, memberLists, joinedMidPull);
                missing.AddRange(memberLists.Where(id => knownContainers?.Contains(id) == false));
            }

            if (response.MemberBudgetIds is { } memberBudgets)
            {
                await ReconcileBudgetsAsync(owner, memberBudgets, joinedMidPull);
                missing.AddRange(memberBudgets.Where(id => knownContainers?.Contains(id) == false));
            }

            if (missing.Count > 0)
            {
                if (await FullPullAsync(response.Marker, missing) is { } whole)
                {
                    pulled += await SaveAsync(whole.Documents);
                }
                else
                {
                    await ForgetAsync(missing);
                }
            }

            if (response.MemberListIds is not null)
            {
                await DropStrandedAsync(owner);
            }

            if (response.MemberBudgetIds is not null)
            {
                await DropStrandedMoneyAsync(owner);
            }
        }

        _freshJoins.ExceptWith(settledJoins);
        await replica.SetMarkerAsync(response.Marker);
        await replica.SetCollectionsFingerprintAsync(CollectionsFingerprint);
        return pulled;
    }

    // A joined container's own row arrives by delta, so only what the replica held before the pull tells a new membership.
    async Task<HashSet<Guid>?> KnownContainersAsync(Guid? me)
    {
        if (me is not { } owner)
        {
            return null;
        }

        var lists = await replica.LoadAllAsync<Module.Tasks.Lists.TaskList>(owner);
        var budgets = await replica.LoadAllAsync<Module.Money.Budgets.Budget>(owner);
        return [.. lists.Select(list => list.Id), .. budgets.Select(budget => budget.Id)];
    }

    async Task ForgetAsync(IEnumerable<Guid> containers)
    {
        foreach (var id in containers)
        {
            await PurgeAsync(id);
            await PurgeBudgetAsync(id);
        }
    }

    // A failing full pull must not stall the marker -- the caller forgets the new containers so the
    // next sync retries them, instead of every delta pull wedging behind it forever.
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

                // A pull begun before a local commit carries the server's older copy; the
                // queued command brings the server up to the local version on the next push.
                if (await replica.SaveUnlessNewerAsync(aggregate))
                {
                    saved++;
                }
            }
        }

        return saved;
    }

    // A membership set read before a join landed cannot speak for the joined container.
    async Task ReconcileAsync(Guid me, IReadOnlyCollection<Guid> memberLists, IReadOnlySet<Guid> joinedMidPull)
    {
        foreach (var lost in (await replica.LoadAllAsync<Module.Tasks.Lists.TaskList>(me))
                     .Where(list => list.UserId != me && !memberLists.Contains(list.Id) && !joinedMidPull.Contains(list.Id)))
        {
            await PurgeAsync(lost.Id);
        }
    }

    async Task ReconcileBudgetsAsync(Guid me, IReadOnlyCollection<Guid> memberBudgets, IReadOnlySet<Guid> joinedMidPull)
    {
        foreach (var lost in (await replica.LoadAllAsync<Module.Money.Budgets.Budget>(me))
                     .Where(budget => budget.UserId != me && !memberBudgets.Contains(budget.Id) && !joinedMidPull.Contains(budget.Id)))
        {
            await PurgeBudgetAsync(lost.Id);
        }
    }

    async Task DropStrandedMoneyAsync(Guid me)
    {
        var held = (await replica.LoadAllAsync<Module.Money.Budgets.Budget>(me)).Select(budget => budget.Id).ToHashSet();

        foreach (var entry in (await replica.LoadAllAsync<Module.Money.Entries.MoneyEntry>(Guid.Empty))
                     .Where(entry => entry.UserId != me && !held.Contains(entry.BudgetId)))
        {
            await replica.RemoveAsync(entry.Id);
        }

        foreach (var snapshot in (await replica.LoadAllAsync<Module.Money.Balance.BalanceSnapshot>(Guid.Empty))
                     .Where(snapshot => snapshot.UserId != me && !held.Contains(snapshot.BudgetId)))
        {
            await replica.RemoveAsync(snapshot.Id);
        }
    }

    async Task PurgeBudgetAsync(Guid budgetId)
    {
        foreach (var entry in (await replica.LoadAllAsync<Module.Money.Entries.MoneyEntry>(Guid.Empty))
                     .Where(entry => entry.BudgetId == budgetId))
        {
            await replica.RemoveAsync(entry.Id);
        }

        foreach (var snapshot in (await replica.LoadAllAsync<Module.Money.Balance.BalanceSnapshot>(Guid.Empty))
                     .Where(snapshot => snapshot.BudgetId == budgetId))
        {
            await replica.RemoveAsync(snapshot.Id);
        }

        await replica.RemoveAsync(budgetId);
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

    public async Task AdoptJoinAsync(JoinOutcome.Joined joined)
    {
        _freshJoins.Add(joined.Id);
        await SaveAsync(joined.Documents);
    }

    public async Task<JoinOutcome> JoinAsync(string token, string code, CancellationToken ct)
    {
        var outcome = await api.JoinAsync(token, code);
        if (outcome is JoinOutcome.Joined joined)
        {
            await AdoptJoinAsync(joined);
        }

        return outcome;
    }
}
