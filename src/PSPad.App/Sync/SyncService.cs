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

    readonly HashSet<Guid> _freshJoins = [];
    readonly SemaphoreSlim _membership = new(1, 1);

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
        ["balancesnapshots"] = typeof(Module.Money.Balance.BalanceSnapshot),
        ["investmentportfolios"] = typeof(Module.Investments.Portfolio.InvestmentPortfolio),
        ["investmentaccounts"] = typeof(Module.Investments.Accounts.InvestmentAccount),
        ["investmentoperations"] = typeof(Module.Investments.Operations.InvestmentOperation),
        ["instruments"] = typeof(Module.Investments.Instruments.Instrument)
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

    public static string CollectionsFingerprint { get; } =
        string.Join(",", Collections.Keys.OrderBy(key => key, StringComparer.Ordinal));

    async Task<int?> PullAsync(CancellationToken ct)
    {
        var storedFingerprint = await replica.CollectionsFingerprintAsync();

        // A client built before a collection existed skipped its rows silently while still
        // advancing its marker past them, so a stale fingerprint forces one full re-pull.
        var since = storedFingerprint == CollectionsFingerprint ? await replica.MarkerAsync() : 0;
        var fromZero = since == 0;
        var pulled = 0;
        var me = await replica.OwnerAsync();
        var owed = (await replica.OwedWholeAsync()).ToHashSet();
        var settledJoins = await SettledJoinsAsync();
        SyncResponse? response;

        while (true)
        {
            response = await api.SyncAsync(since, []);

            if (response is null)
            {
                return null;
            }

            if (me is not null && !fromZero)
            {
                await OweNewContainersAsync(response, owed, me.Value);
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
            pulled += await ReconcileMembershipAsync(owner, response, settledJoins);
            pulled += await PullOwedWholeAsync(response, owed, fromZero);
        }

        await _membership.WaitAsync();
        _freshJoins.ExceptWith(settledJoins);
        _membership.Release();

        await replica.SetMarkerAsync(response.Marker);
        await replica.SetCollectionsFingerprintAsync(CollectionsFingerprint);
        return pulled;
    }

    async Task<Guid[]> SettledJoinsAsync()
    {
        await _membership.WaitAsync();
        try
        {
            return [.. _freshJoins];
        }
        finally
        {
            _membership.Release();
        }
    }

    // A joined container's row arrives by delta but its older children do not, so it is owed a whole pull until saved.
    async Task OweNewContainersAsync(SyncResponse page, HashSet<Guid> owed, Guid me)
    {
        var found = false;
        var memberLists = page.MemberListIds ?? [];
        var memberBudgets = page.MemberBudgetIds ?? [];

        if (memberLists.Any())
        {
            var held = (await replica.LoadAllAsync<Module.Tasks.Lists.TaskList>(me)).Select(list => list.Id).ToHashSet();

            foreach (var id in memberLists.Where(id => !held.Contains(id)))
            {
                found |= owed.Add(id);
            }
        }

        if (memberBudgets.Any())
        {
            var held = (await replica.LoadAllAsync<Module.Money.Budgets.Budget>(me)).Select(budget => budget.Id).ToHashSet();

            foreach (var id in memberBudgets.Where(id => !held.Contains(id)))
            {
                found |= owed.Add(id);
            }
        }

        if (found)
        {
            await replica.SetOwedWholeAsync(owed);
        }
    }

    async Task<int> ReconcileMembershipAsync(Guid owner, SyncResponse response, IReadOnlyCollection<Guid> settledJoins)
    {
        await _membership.WaitAsync();
        try
        {
            var joinedMidPull = _freshJoins.Except(settledJoins).ToHashSet();
            var removed = 0;

            if (response.MemberListIds is { } memberLists)
            {
                removed += await ReconcileAsync(owner, memberLists, joinedMidPull);
                removed += await DropStrandedAsync(owner);
            }

            if (response.MemberBudgetIds is { } memberBudgets)
            {
                removed += await ReconcileBudgetsAsync(owner, memberBudgets, joinedMidPull);
                removed += await DropStrandedMoneyAsync(owner);
            }

            return removed;
        }
        finally
        {
            _membership.Release();
        }
    }

    async Task<int> PullOwedWholeAsync(SyncResponse response, HashSet<Guid> owed, bool fromZero)
    {
        var stillOwed = owed.Count;

        if (fromZero)
        {
            owed.Clear();
        }

        owed.IntersectWith([.. response.MemberListIds ?? [], .. response.MemberBudgetIds ?? []]);
        var pulled = 0;

        if (owed.Count > 0 && await FullPullAsync(response.Marker, [.. owed]) is { } whole)
        {
            pulled = await SaveAsync(whole.Documents);
            owed.Clear();
        }

        if (owed.Count != stillOwed)
        {
            await replica.SetOwedWholeAsync(owed);
        }

        return pulled;
    }

    // A failing full pull must not stall the marker -- what it would fetch stays owed and is retried next sync.
    async Task<SyncResponse?> FullPullAsync(long marker, IReadOnlyCollection<Guid> owed)
    {
        try
        {
            return await api.SyncAsync(marker, owed);
        }
        catch (Exception failure) when (failure is HttpRequestException or OperationCanceledException or JsonException)
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
    async Task<int> ReconcileAsync(Guid me, IReadOnlyCollection<Guid> memberLists, IReadOnlySet<Guid> joinedMidPull)
    {
        var removed = 0;

        foreach (var lost in (await replica.LoadAllAsync<Module.Tasks.Lists.TaskList>(me))
                     .Where(list => list.UserId != me && !memberLists.Contains(list.Id) && !joinedMidPull.Contains(list.Id)))
        {
            removed += await PurgeAsync(lost.Id);
        }

        return removed;
    }

    async Task<int> ReconcileBudgetsAsync(Guid me, IReadOnlyCollection<Guid> memberBudgets, IReadOnlySet<Guid> joinedMidPull)
    {
        var removed = 0;

        foreach (var lost in (await replica.LoadAllAsync<Module.Money.Budgets.Budget>(me))
                     .Where(budget => budget.UserId != me && !memberBudgets.Contains(budget.Id) && !joinedMidPull.Contains(budget.Id)))
        {
            removed += await PurgeBudgetAsync(lost.Id);
        }

        return removed;
    }

    async Task<int> DropStrandedMoneyAsync(Guid me)
    {
        var removed = 0;
        var held = (await replica.LoadAllAsync<Module.Money.Budgets.Budget>(me)).Select(budget => budget.Id).ToHashSet();

        foreach (var entry in (await replica.LoadAllAsync<Module.Money.Entries.MoneyEntry>(Guid.Empty))
                     .Where(entry => entry.UserId != me && !held.Contains(entry.BudgetId)))
        {
            await replica.RemoveAsync(entry.Id);
            removed++;
        }

        foreach (var snapshot in (await replica.LoadAllAsync<Module.Money.Balance.BalanceSnapshot>(Guid.Empty))
                     .Where(snapshot => snapshot.UserId != me && !held.Contains(snapshot.BudgetId)))
        {
            await replica.RemoveAsync(snapshot.Id);
            removed++;
        }

        return removed;
    }

    async Task<int> PurgeBudgetAsync(Guid budgetId)
    {
        var removed = 0;

        foreach (var entry in (await replica.LoadAllAsync<Module.Money.Entries.MoneyEntry>(Guid.Empty))
                     .Where(entry => entry.BudgetId == budgetId))
        {
            await replica.RemoveAsync(entry.Id);
            removed++;
        }

        foreach (var snapshot in (await replica.LoadAllAsync<Module.Money.Balance.BalanceSnapshot>(Guid.Empty))
                     .Where(snapshot => snapshot.BudgetId == budgetId))
        {
            await replica.RemoveAsync(snapshot.Id);
            removed++;
        }

        await replica.RemoveAsync(budgetId);
        removed++;

        return removed;
    }

    // A row the owner moved out of a shared list arrives once, naming a list this device never holds.
    async Task<int> DropStrandedAsync(Guid me)
    {
        var removed = 0;
        var held = (await replica.LoadAllAsync<Module.Tasks.Lists.TaskList>(me)).Select(list => list.Id).ToHashSet();

        foreach (var task in (await replica.LoadAllAsync<Module.Tasks.Tasks.TodoTask>(Guid.Empty))
                     .Where(task => task.UserId != me && !held.Contains(task.ListId)))
        {
            await replica.RemoveAsync(task.Id);
            removed++;
        }

        foreach (var item in (await replica.LoadAllAsync<Module.Tasks.References.ReferenceItem>(Guid.Empty))
                     .Where(item => item.UserId != me && !held.Contains(item.ListId)))
        {
            await replica.RemoveAsync(item.Id);
            removed++;
        }

        return removed;
    }

    async Task<int> PurgeAsync(Guid listId)
    {
        var removed = 0;

        foreach (var task in (await replica.LoadAllAsync<Module.Tasks.Tasks.TodoTask>(Guid.Empty))
                     .Where(task => task.ListId == listId))
        {
            await replica.RemoveAsync(task.Id);
            removed++;
        }

        foreach (var item in (await replica.LoadAllAsync<Module.Tasks.References.ReferenceItem>(Guid.Empty))
                     .Where(item => item.ListId == listId))
        {
            await replica.RemoveAsync(item.Id);
            removed++;
        }

        await replica.RemoveAsync(listId);
        removed++;

        return removed;
    }

    public async Task AdoptJoinAsync(JoinOutcome.Joined joined)
    {
        await _membership.WaitAsync();
        try
        {
            _freshJoins.Add(joined.Id);

            // Owed until every document is saved, so a reload partway through still pulls the container whole.
            await replica.SetOwedWholeAsync([.. await replica.OwedWholeAsync(), joined.Id]);
            await SaveAsync(joined.Documents);
            await replica.SetOwedWholeAsync([.. (await replica.OwedWholeAsync()).Where(id => id != joined.Id)]);
        }
        finally
        {
            _membership.Release();
        }
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
