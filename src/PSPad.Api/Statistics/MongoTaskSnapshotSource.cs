using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Statistics;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Api.Statistics;

public sealed class MongoTaskSnapshotSource(MongoContext context) : ITaskSnapshotSource
{
    public async Task<IReadOnlyDictionary<Guid, TaskSnapshot>> CurrentAsync(
        IReadOnlyCollection<Guid> taskIds, CancellationToken ct)
    {
        var tasks = await context.Collection<TodoTask>()
            .Find(Builders<TodoTask>.Filter.In(task => task.Id, taskIds))
            .ToListAsync(ct);

        var snapshots = tasks.ToDictionary(task => task.Id, ToSnapshot);

        foreach (var taskId in taskIds)
        {
            snapshots.TryAdd(taskId, new TaskSnapshot(PSPad.Module.Statistics.TaskStatus.Gone, ""));
        }

        return snapshots;
    }

    static TaskSnapshot ToSnapshot(TodoTask task) =>
        task.Deleted
            ? new TaskSnapshot(PSPad.Module.Statistics.TaskStatus.Gone, task.Name)
            : task.CompletedAt is not null
                ? new TaskSnapshot(PSPad.Module.Statistics.TaskStatus.Done, task.Name)
                : new TaskSnapshot(PSPad.Module.Statistics.TaskStatus.Open, task.Name);
}
