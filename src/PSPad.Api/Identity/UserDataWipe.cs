using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;

namespace PSPad.Api.Identity;

public static class UserDataWipe
{
    public static async Task RunAsync(MongoContext context, Guid userId, CancellationToken ct)
    {
        var collectionNames = await (await context.Database.ListCollectionNamesAsync(cancellationToken: ct))
            .ToListAsync(ct);

        using var session = await context.Client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();

        try
        {
            var sequence = new SequenceSource(context);
            var lists = context.Collection<BsonDocument>("tasklists");
            var memberships = await lists
                .Find(session, Builders<BsonDocument>.Filter.Eq("_members.userId", userId))
                .Project(Builders<BsonDocument>.Projection.Include("_id"))
                .ToListAsync(ct);

            foreach (var membership in memberships)
            {
                await lists.UpdateOneAsync(
                    session,
                    Builders<BsonDocument>.Filter.Eq("_id", membership["_id"]),
                    Builders<BsonDocument>.Update
                        .PullFilter("_members", Builders<BsonDocument>.Filter.Eq("userId", userId))
                        .Set("seq", await sequence.NextAsync(session, ct)),
                    cancellationToken: ct);
            }

            foreach (var name in collectionNames)
            {
                await context.Collection<BsonDocument>(name).DeleteManyAsync(
                    session, Builders<BsonDocument>.Filter.Eq("userId", userId), cancellationToken: ct);
            }

            await session.CommitTransactionAsync(ct);
        }
        catch
        {
            await session.AbortTransactionAsync(ct);
            throw;
        }
    }
}
