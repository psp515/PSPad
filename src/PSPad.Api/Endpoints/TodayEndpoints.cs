using PSPad.Abstractions;
using PSPad.Api.Identity;
using PSPad.Module.Identity;
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
            CancellationToken ct) =>
        {
            var user = await users.LoadAsync(current.UserId, ct);
            var zone = TimeZoneInfo.FindSystemTimeZoneById(user?.TimeZone ?? "Etc/UTC");
            var today = TodayRule.TodayIn(clock.UtcNow, zone);
            var all = await tasks.LoadAllAsync(current.UserId, ct);

            return Results.Ok(TodayRule.Select(all, today));
        });
    }
}
