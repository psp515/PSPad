using System.Text.Json;
using MongoDB.Driver;
using PSPad.Abstractions;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Identity;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Api.Sync;

public sealed class SyncReader(MongoContext context)
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        IncludeFields = true
    };

    public async Task<SyncResponse> ReadAsync(Guid userId, long since, CancellationToken ct)
    {
        using var session = await context.Client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction(new TransactionOptions(readConcern: ReadConcern.Snapshot));

        try
        {
            var response = await ReadWithinSessionAsync(session, userId, since, ct);
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
        IClientSessionHandle session, Guid userId, long since, CancellationToken ct)
    {
        var events = await context.Collection<StoredEvent>("events")
            .Find(session,
                Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, userId) &
                Builders<StoredEvent>.Filter.Gt(entry => entry.Seq, since))
            .SortBy(entry => entry.Seq)
            .ToListAsync(ct);

        var areas = await ReadCollectionAsync<Area>(session, userId, since, ct);
        var taskLists = await ReadCollectionAsync<TaskList>(session, userId, since, ct);
        var todoTasks = await ReadCollectionAsync<TodoTask>(session, userId, since, ct);
        var goals = await ReadCollectionAsync<Goal>(session, userId, since, ct);
        var inboxes = await ReadCollectionAsync<Inbox>(session, userId, since, ct);
        var users = await ReadCollectionAsync<User>(session, userId, since, ct);

        var documents = new Dictionary<string, JsonElement[]>
        {
            ["areas"] = areas.Rows,
            ["tasklists"] = taskLists.Rows,
            ["todotasks"] = todoTasks.Rows,
            ["goals"] = goals.Rows,
            ["inboxes"] = inboxes.Rows,
            ["users"] = users.Rows
        };

        var highestDocumentSeq = new[]
        {
            areas.HighestSeq, taskLists.HighestSeq, todoTasks.HighestSeq,
            goals.HighestSeq, inboxes.HighestSeq, users.HighestSeq
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
                .ToArray());
    }

    async Task<(JsonElement[] Rows, long HighestSeq)> ReadCollectionAsync<T>(
        IClientSessionHandle session, Guid userId, long since, CancellationToken ct)
        where T : Aggregate
    {
        var rows = await context.Collection<T>()
            .Find(session,
                Builders<T>.Filter.Eq(document => document.UserId, userId) &
                Builders<T>.Filter.Gt(document => document.Seq, since))
            .ToListAsync(ct);

        var highestSeq = rows.Count > 0 ? rows.Max(row => row.Seq) : 0;

        return (rows.Select(row => JsonSerializer.SerializeToElement(row, JsonOptions)).ToArray(), highestSeq);
    }

    async Task<long> HighestSeqAsync(IClientSessionHandle session, Guid userId, CancellationToken ct)
    {
        var newest = await context.Collection<StoredEvent>("events")
            .Find(session, Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, userId))
            .SortByDescending(entry => entry.Seq)
            .FirstOrDefaultAsync(ct);

        return newest?.Seq ?? 0;
    }
}
