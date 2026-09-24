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
