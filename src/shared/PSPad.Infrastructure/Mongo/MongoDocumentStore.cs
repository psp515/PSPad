using MongoDB.Driver;
using PSPad.Abstractions;

namespace PSPad.Infrastructure.Mongo;

public sealed class MongoDocumentStore<T>(MongoContext context) : IDocumentStore<T> where T : Aggregate
{
    public async Task<T?> LoadAsync(Guid id, CancellationToken ct) =>
        await context.Collection<T>()
            .Find(Builders<T>.Filter.Eq(document => document.Id, id))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<T>> LoadAllAsync(Guid userId, CancellationToken ct) =>
        await context.Collection<T>()
            .Find(Builders<T>.Filter.Eq(document => document.UserId, userId))
            .ToListAsync(ct);
}
