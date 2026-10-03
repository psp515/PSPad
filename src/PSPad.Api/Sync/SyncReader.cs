using System.Text.Json;
using MongoDB.Driver;
using PSPad.Abstractions;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Identity;
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

    public async Task<SyncResponse> ReadAsync(
        Guid userId, long since, IReadOnlyCollection<Guid> full, CancellationToken ct)
    {
        using var session = await context.Client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction(new TransactionOptions(readConcern: ReadConcern.Snapshot));

        try
        {
            var response = await ReadWithinSessionAsync(session, userId, since, full, ct);
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
        IClientSessionHandle session, Guid userId, long since, IReadOnlyCollection<Guid> full, CancellationToken ct)
    {
        var events = await context.Collection<StoredEvent>("events")
            .Find(session,
                Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, userId) &
                Builders<StoredEvent>.Filter.Gt(entry => entry.Seq, since))
            .SortBy(entry => entry.Seq)
            .ToListAsync(ct);

        var memberListIds = await context.Collection<TaskList>()
            .Find(session,
                Builders<TaskList>.Filter.Eq("_members.userId", userId) &
                Builders<TaskList>.Filter.Eq(list => list.Deleted, false))
            .Project(list => list.Id)
            .ToListAsync(ct);

        var visible = new HashSet<Guid>(memberListIds);
        var wholeLists = full.Where(visible.Contains).ToArray();

        bool Readable(Guid ownerId, Guid listId) => ownerId == userId || visible.Contains(listId);

        var areas = await ReadAsync<Area>(session, since, Owned<Area>(userId), None<Area>(), ct);
        var taskLists = await ReadAsync(session, since,
            Owned<TaskList>(userId) | Builders<TaskList>.Filter.Eq("_members.userId", userId),
            Builders<TaskList>.Filter.In(list => list.Id, wholeLists), ct);
        var todoTasks = await ReadAsync(session, since,
            Owned<TodoTask>(userId) |
            Builders<TodoTask>.Filter.In(task => task.ListId, memberListIds) |
            Builders<TodoTask>.Filter.In(task => task.PreviousListId, memberListIds.Select(id => (Guid?)id)),
            Builders<TodoTask>.Filter.In(task => task.ListId, wholeLists), ct,
            task => Readable(task.UserId, task.ListId)
                ? Serialize(task)
                : Stub(task, task.ListId, task.PreviousListId));
        var goals = await ReadAsync<Goal>(session, since, Owned<Goal>(userId), None<Goal>(), ct);
        var inboxes = await ReadAsync<Inbox>(session, since, Owned<Inbox>(userId), None<Inbox>(), ct);
        var users = await ReadAsync<User>(session, since, Owned<User>(userId), None<User>(), ct);
        var referenceItems = await ReadAsync(session, since,
            Owned<ReferenceItem>(userId) |
            Builders<ReferenceItem>.Filter.In(item => item.ListId, memberListIds) |
            Builders<ReferenceItem>.Filter.In(item => item.PreviousListId, memberListIds.Select(id => (Guid?)id)),
            Builders<ReferenceItem>.Filter.In(item => item.ListId, wholeLists), ct,
            item => Readable(item.UserId, item.ListId)
                ? Serialize(item)
                : Stub(item, item.ListId, item.PreviousListId));
        var areaViews = await ReadAsync<AreaView>(session, since, Owned<AreaView>(userId), None<AreaView>(), ct);
        var listViews = await ReadAsync<ListView>(session, since, Owned<ListView>(userId), None<ListView>(), ct);

        var documents = new Dictionary<string, JsonElement[]>
        {
            ["areas"] = areas.Rows,
            ["tasklists"] = taskLists.Rows,
            ["todotasks"] = todoTasks.Rows,
            ["goals"] = goals.Rows,
            ["inboxes"] = inboxes.Rows,
            ["users"] = users.Rows,
            ["referenceitems"] = referenceItems.Rows,
            ["areaviews"] = areaViews.Rows,
            ["listviews"] = listViews.Rows
        };

        var highestDocumentSeq = new[]
        {
            areas.HighestSeq, taskLists.HighestSeq, todoTasks.HighestSeq,
            goals.HighestSeq, inboxes.HighestSeq, users.HighestSeq, referenceItems.HighestSeq,
            areaViews.HighestSeq, listViews.HighestSeq
        }.Max();

        var highestEventSeq = events.Count > 0
            ? events[^1].Seq
            : Math.Max(since, await HighestSeqAsync(session, userId, ct));

        var marker = Math.Max(highestEventSeq, highestDocumentSeq);

        return new SyncResponse(
            marker,
            documents,
            events.Select(entry => new SyncEvent(
                entry.Seq, entry.AggregateType, entry.AggregateId, entry.Type, entry.Payload, entry.At))
                .ToArray(),
            [.. memberListIds]);
    }

    async Task<(JsonElement[] Rows, long HighestSeq)> ReadAsync<T>(
        IClientSessionHandle session, long since, FilterDefinition<T> visible, FilterDefinition<T> whole,
        CancellationToken ct, Func<T, JsonElement>? project = null)
        where T : Aggregate
    {
        var rows = await context.Collection<T>()
            .Find(session, (visible & Builders<T>.Filter.Gt(document => document.Seq, since)) | whole)
            .ToListAsync(ct);

        var highestSeq = rows.Count > 0 ? rows.Max(row => row.Seq) : 0;

        return (rows.Select(project ?? Serialize).ToArray(), highestSeq);
    }

    static JsonElement Serialize<T>(T row) where T : Aggregate =>
        JsonSerializer.SerializeToElement(row, JsonOptions);

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

    static FilterDefinition<T> None<T>() where T : Aggregate =>
        Builders<T>.Filter.In(document => document.Id, Array.Empty<Guid>());
}
