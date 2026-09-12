using PSPad.Abstractions;
using PSPad.Api.Identity;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Today;

namespace PSPad.Api.Endpoints;

public static class TodayEndpoints
{
    public static void MapTodayEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/today", async (
            HttpRequest request,
            IDocumentStore<TodoTask> tasks,
            ICurrentUser user,
            IClock clock,
            CancellationToken ct) =>
        {
            var zone = ZoneFrom(request.Headers["X-Time-Zone"]);
            var today = TodayRule.TodayIn(clock.UtcNow, zone);
            var all = await tasks.LoadAllAsync(user.UserId, ct);

            return Results.Ok(TodayRule.Select(all, today));
        });
    }

    static TimeZoneInfo ZoneFrom(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
