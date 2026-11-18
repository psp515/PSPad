using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Identity;
using PSPad.Module.Reminders;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Api.Reminders;

public sealed class MongoReminderSource(MongoContext context) : IReminderSource
{
    public async Task<IReadOnlyList<ReminderCandidate>> CandidatesAsync(CancellationToken ct)
    {
        var tasks = await context.Collection<TodoTask>()
            .Find(Builders<TodoTask>.Filter.Ne("reminder", BsonNull.Value) &
                  Builders<TodoTask>.Filter.Eq(task => task.Deleted, false))
            .ToListAsync(ct);
        if (tasks.Count == 0)
        {
            return [];
        }

        var listIds = tasks.Select(task => task.ListId).Distinct().ToArray();
        var lists = (await context.Collection<TaskList>()
                .Find(Builders<TaskList>.Filter.In(list => list.Id, listIds))
                .ToListAsync(ct))
            .Where(list => !list.Deleted)
            .ToDictionary(list => list.Id);

        var userIds = lists.Values.SelectMany(Audience).Distinct().ToArray();
        var recipients = (await context.Collection<User>()
                .Find(Builders<User>.Filter.In(user => user.Id, userIds))
                .ToListAsync(ct))
            .Where(user => !user.Deleted)
            .ToDictionary(user => user.Id, user => new Recipient(user.Id, user.TimeZone, user.Language));

        return tasks
            .Where(task => lists.ContainsKey(task.ListId))
            .Select(task => new ReminderCandidate(
                task,
                [.. Audience(lists[task.ListId]).Distinct().Where(recipients.ContainsKey).Select(id => recipients[id])]))
            .ToArray();
    }

    static IEnumerable<Guid> Audience(TaskList list) =>
        list.Members.Select(member => member.UserId).Prepend(list.UserId);
}
