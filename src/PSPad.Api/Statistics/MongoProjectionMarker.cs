using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Statistics;

namespace PSPad.Api.Statistics;

public sealed class MongoProjectionMarker(MongoContext context) : IProjectionMarker
{
    const string DocumentId = "statistics";
    const string Field = "lastProcessedSeq";

    static readonly FilterDefinition<BsonDocument> TheMarker =
        Builders<BsonDocument>.Filter.Eq("_id", DocumentId);

    IMongoCollection<BsonDocument> State => context.Collection<BsonDocument>("statistics_state");

    public async Task<long> ReadAsync(CancellationToken ct)
    {
        var marker = await State.Find(TheMarker).FirstOrDefaultAsync(ct);

        return marker is null ? 0 : marker.GetValue(Field, 0L).ToInt64();
    }

    public Task WriteAsync(long seq, CancellationToken ct) =>
        State.UpdateOneAsync(
            TheMarker,
            Builders<BsonDocument>.Update.Set(Field, seq),
            new UpdateOptions { IsUpsert = true },
            ct);

    public async Task<bool> IsBehindAsync(int version, CancellationToken ct)
    {
        var state = await State.Find(TheMarker).FirstOrDefaultAsync(ct);
        var stored = state?.GetValue("projectionVersion", 1).ToInt32() ?? 0;

        return stored < version;
    }

    public Task AdoptVersionAsync(int version, CancellationToken ct) =>
        State.UpdateOneAsync(
            TheMarker,
            Builders<BsonDocument>.Update.Set(Field, 0L).Set("projectionVersion", version),
            new UpdateOptions { IsUpsert = true },
            ct);
}
