using MongoDB.Driver;
using PSPad.Abstractions;
using PSPad.Api.Identity;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Identity;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Today;

namespace PSPad.Api.Endpoints;

public static class TodayEndpoints
{
    public static void MapTodayEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("today", async (
            IDocumentStore<User> users,
            IDocumentStore<TodoTask> tasks,
            ICurrentUser current,
            IClock clock,
            MongoContext context,
            CancellationToken ct) =>
        {
            var user = await users.LoadAsync(current.UserId, ct);
            var zone = TimeZoneInfo.FindSystemTimeZoneById(user?.TimeZone ?? "Etc/UTC");
            var today = TodayRule.TodayIn(clock.UtcNow, zone);
            var all = await tasks.LoadAllAsync(current.UserId, ct);

            var memberLists = await context.Collection<TaskList>()
                .Find(Builders<TaskList>.Filter.Eq("_members.userId", current.UserId) &
                      Builders<TaskList>.Filter.Eq(list => list.Deleted, false))
                .Project(list => list.Id)
                .ToListAsync(ct);
            var shared = memberLists.Count == 0
                ? []
                : await context.Collection<TodoTask>()
                    .Find(Builders<TodoTask>.Filter.In(task => task.ListId, memberLists) &
                          Builders<TodoTask>.Filter.Eq(task => task.Deleted, false))
                    .ToListAsync(ct);

            var merged = all.Concat(shared).DistinctBy(task => task.Id).ToArray();
            return Results.Ok(TodayRule.Select(merged, today));
        });
    }
}
