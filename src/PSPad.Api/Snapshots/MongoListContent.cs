using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Sharing.Ports;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Api.Snapshots;

public sealed class MongoListContent(MongoContext context) : IListContent
{
    public async Task<(TaskList? List, IReadOnlyList<TodoTask> Tasks, IReadOnlyList<ReferenceItem> Items)> LoadAsync(
        Guid listId, CancellationToken ct)
    {
        var list = await context.Collection<TaskList>()
            .Find(Builders<TaskList>.Filter.Eq(document => document.Id, listId))
            .FirstOrDefaultAsync(ct);

        var tasks = await context.Collection<TodoTask>()
            .Find(Builders<TodoTask>.Filter.Eq(document => document.ListId, listId) &
                  Builders<TodoTask>.Filter.Eq(document => document.Deleted, false))
            .ToListAsync(ct);

        var items = await context.Collection<ReferenceItem>()
            .Find(Builders<ReferenceItem>.Filter.Eq(document => document.ListId, listId) &
                  Builders<ReferenceItem>.Filter.Eq(document => document.Deleted, false))
            .ToListAsync(ct);

        return (list, tasks, items);
    }
}
