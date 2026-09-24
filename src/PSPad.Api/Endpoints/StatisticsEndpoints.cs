using PSPad.Abstractions;
using PSPad.Api.Identity;
using PSPad.Module.Identity;
using PSPad.Module.Statistics;
using PSPad.Module.Tasks.Today;

namespace PSPad.Api.Endpoints;

public static class StatisticsEndpoints
{
    public static void MapStatisticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("statistics/records", async (
            long? before,
            int? limit,
            StatisticsReader reader,
            ICurrentUser current,
            CancellationToken ct) =>
            Results.Ok(await reader.ReadAsync(current.UserId, before, limit ?? 50, ct)));

        app.MapGet("statistics/overview", async (
            int? days,
            StatisticsOverviewReader reader,
            IDocumentStore<User> users,
            ICurrentUser current,
            IClock clock,
            CancellationToken ct) =>
        {
            var user = await users.LoadAsync(current.UserId, ct);
            var zone = TimeZoneInfo.FindSystemTimeZoneById(user?.TimeZone ?? "Etc/UTC");
            var today = TodayRule.TodayIn(clock.UtcNow, zone);

            return Results.Ok(await reader.ReadAsync(current.UserId, today, days ?? 30, zone, ct));
        });
    }
}
