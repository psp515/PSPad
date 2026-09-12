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
        var events = await context.Collection<StoredEvent>("events")
            .Find(Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, userId) &
                  Builders<StoredEvent>.Filter.Gt(entry => entry.Seq, since))
            .SortBy(entry => entry.Seq)
            .ToListAsync(ct);

        var documents = new Dictionary<string, JsonElement[]>
        {
            ["areas"] = await ReadCollectionAsync<Area>(userId, since, ct),
            ["tasklists"] = await ReadCollectionAsync<TaskList>(userId, since, ct),
            ["todotasks"] = await ReadCollectionAsync<TodoTask>(userId, since, ct),
            ["goals"] = await ReadCollectionAsync<Goal>(userId, since, ct),
            ["inboxes"] = await ReadCollectionAsync<Inbox>(userId, since, ct),
            ["users"] = await ReadCollectionAsync<User>(userId, since, ct)
        };

        var marker = events.Count > 0
            ? events[^1].Seq
            : Math.Max(since, await HighestSeqAsync(userId, ct));

        return new SyncResponse(
            marker,
            documents,
            events.Select(entry => new SyncEvent(
                entry.Seq, entry.AggregateType, entry.AggregateId, entry.Type, entry.Payload, entry.At))
                .ToArray());
    }

    async Task<JsonElement[]> ReadCollectionAsync<T>(Guid userId, long since, CancellationToken ct)
        where T : Aggregate
    {
        var rows = await context.Collection<T>()
            .Find(Builders<T>.Filter.Eq(document => document.UserId, userId) &
                  Builders<T>.Filter.Gt(document => document.Seq, since))
            .ToListAsync(ct);

        return rows.Select(row => JsonSerializer.SerializeToElement(row, JsonOptions)).ToArray();
    }

    async Task<long> HighestSeqAsync(Guid userId, CancellationToken ct)
    {
        var newest = await context.Collection<StoredEvent>("events")
            .Find(Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, userId))
            .SortByDescending(entry => entry.Seq)
            .FirstOrDefaultAsync(ct);

        return newest?.Seq ?? 0;
    }
}
