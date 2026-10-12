using System.Text.Json;
using System.Text.Json.Nodes;
using MongoDB.Driver;
using PSPad.Abstractions;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Identity;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Preferences;
using PSPad.Module.Presentation.AreaViews;
using PSPad.Module.Presentation.ListViews;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Api.Sync;

public sealed class SyncReader(MongoContext context)
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        IncludeFields = true
    };

    static readonly string[] InviteSecrets =
        ["inviteToken", "inviteCode", "inviteExpiresAt", "wrongCodes", "closedByWrongCodes"];

    public const int MaxPageSize = 1000;

    public async Task<SyncResponse> ReadAsync(
        Guid userId, long since, IReadOnlyCollection<Guid> full, CancellationToken ct, int limit = MaxPageSize)
    {
        using var session = await context.Client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction(new TransactionOptions(readConcern: ReadConcern.Snapshot));

        try
        {
            var response = await ReadWithinSessionAsync(
                session, userId, since, full, Math.Clamp(limit, 1, MaxPageSize), ct);
            await session.CommitTransactionAsync(ct);
            return response;
        }
        catch
        {
            await session.AbortTransactionAsync(ct);
            throw;
        }
    }

    async Task<SyncResponse> ReadWithinSessionAsync(
        IClientSessionHandle session, Guid userId, long since, IReadOnlyCollection<Guid> full, int limit,
        CancellationToken ct)
    {
        var memberListIds = await context.Collection<TaskList>()
            .Find(session,
                Builders<TaskList>.Filter.Eq("_members.userId", userId) &
                Builders<TaskList>.Filter.Eq(list => list.Deleted, false))
            .Project(list => list.Id)
            .ToListAsync(ct);

        var visible = new HashSet<Guid>(memberListIds);
        var wholeLists = full.Where(visible.Contains).ToArray();

        bool Readable(Guid ownerId, Guid listId) => ownerId == userId || visible.Contains(listId);

        Task<SyncSource> Read<T>(
            string collection, FilterDefinition<T> visible, FilterDefinition<T>? whole = null,
            Func<T, JsonElement>? project = null) where T : Aggregate =>
            ReadSourceAsync(session, collection, visible, whole, since, limit, project ?? Serialize, ct);

        var sources = new SyncSource[]
        {
            await Read("areas", Owned<Area>(userId)),
            await Read("tasklists",
                Owned<TaskList>(userId) | Builders<TaskList>.Filter.Eq("_members.userId", userId),
                WholeOrNone(Builders<TaskList>.Filter.In(list => list.Id, wholeLists), wholeLists),
                list => list.UserId == userId ? Serialize(list) : WithoutInvite(Serialize(list))),
            await Read("todotasks",
                Owned<TodoTask>(userId) |
                Builders<TodoTask>.Filter.In(task => task.ListId, memberListIds) |
                Builders<TodoTask>.Filter.In(task => task.PreviousListId, memberListIds.Select(id => (Guid?)id)),
                WholeOrNone(Builders<TodoTask>.Filter.In(task => task.ListId, wholeLists), wholeLists),
                task => Readable(task.UserId, task.ListId)
                    ? Serialize(task)
                    : Stub(task, task.ListId, task.PreviousListId)),
            await Read("goals", Owned<Goal>(userId)),
            await Read("inboxes", Owned<Inbox>(userId)),
            await Read("users", Owned<User>(userId)),
            await Read("referenceitems",
                Owned<ReferenceItem>(userId) |
                Builders<ReferenceItem>.Filter.In(item => item.ListId, memberListIds) |
                Builders<ReferenceItem>.Filter.In(item => item.PreviousListId, memberListIds.Select(id => (Guid?)id)),
                WholeOrNone(Builders<ReferenceItem>.Filter.In(item => item.ListId, wholeLists), wholeLists),
                item => Readable(item.UserId, item.ListId)
                    ? Serialize(item)
                    : Stub(item, item.ListId, item.PreviousListId)),
            await Read("areaviews", Owned<AreaView>(userId)),
            await Read("listviews", Owned<ListView>(userId)),
            await Read("budgets", Owned<Budget>(userId)),
            await Read("moneypreferences", Owned<MoneyPreferences>(userId)),
            await Read("moneyentries", Owned<MoneyEntry>(userId)),
            await Read("balancesnapshots", Owned<BalanceSnapshot>(userId))
        };

        // A full source stops at its own last seq; every source is cut there so no row past it is skipped.
        var bounded = sources.Where(source => source.Full).ToArray();
        var hasMore = bounded.Length > 0;
        var upTo = hasMore ? bounded.Min(source => source.LastSeq) : long.MaxValue;

        var documents = sources.ToDictionary(
            source => source.Collection,
            source => source.Rows.Where(row => row.Seq <= upTo || row.Whole).Select(row => row.Json).ToArray());

        var marker = hasMore
            ? upTo
            : Math.Max(
                Math.Max(since, await HighestSeqAsync(session, userId, ct)),
                sources.Max(source => source.LastSeq));

        return new SyncResponse(marker, documents, [.. memberListIds], hasMore);
    }

    async Task<SyncSource> ReadSourceAsync<T>(
        IClientSessionHandle session, string collection, FilterDefinition<T> visible, FilterDefinition<T>? whole,
        long since, int limit, Func<T, JsonElement> project, CancellationToken ct)
        where T : Aggregate
    {
        var changed = await context.Collection<T>()
            .Find(session, visible & Builders<T>.Filter.Gt(document => document.Seq, since))
            .SortBy(document => document.Seq)
            .Limit(limit)
            .ToListAsync(ct);

        var lists = whole is null ? [] : await context.Collection<T>().Find(session, whole).ToListAsync(ct);
        var inLists = lists.Select(row => row.Id).ToHashSet();
        var already = changed.Select(row => row.Id).ToHashSet();

        var rows = changed
            .Select(row => new SyncRow(row.Id, row.Seq, project(row), inLists.Contains(row.Id)))
            .Concat(lists
                .Where(row => !already.Contains(row.Id))
                .Select(row => new SyncRow(row.Id, row.Seq, project(row), true)))
            .ToArray();

        return new SyncSource(
            collection, rows, changed.Count == limit, changed.Count > 0 ? changed[^1].Seq : 0);
    }

    static FilterDefinition<T>? WholeOrNone<T>(FilterDefinition<T> filter, Guid[] lists) =>
        lists.Length > 0 ? filter : null;

    static JsonElement Serialize<T>(T row) where T : Aggregate =>
        JsonSerializer.SerializeToElement(row, JsonOptions);

    static JsonElement WithoutInvite(JsonElement list)
    {
        var row = JsonNode.Parse(list.GetRawText())!.AsObject();
        foreach (var secret in InviteSecrets)
        {
            row.Remove(secret);
        }

        return JsonSerializer.SerializeToElement(row, JsonOptions);
    }

    // A row reached only through previousListId left the caller's lists; it carries just enough to be dropped.
    static JsonElement Stub(Aggregate row, Guid listId, Guid? previousListId) =>
        JsonSerializer.SerializeToElement(new
        {
            row.Id,
            row.UserId,
            ListId = listId,
            PreviousListId = previousListId,
            row.Version,
            row.Deleted,
            row.Seq
        }, JsonOptions);

    async Task<long> HighestSeqAsync(IClientSessionHandle session, Guid userId, CancellationToken ct)
    {
        var newest = await context.Collection<StoredEvent>("events")
            .Find(session, Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, userId))
            .SortByDescending(entry => entry.Seq)
            .FirstOrDefaultAsync(ct);

        return newest?.Seq ?? 0;
    }

    static FilterDefinition<T> Owned<T>(Guid userId) where T : Aggregate =>
        Builders<T>.Filter.Eq(document => document.UserId, userId);

}
