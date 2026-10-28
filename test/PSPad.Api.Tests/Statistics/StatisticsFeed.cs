using System.Net.Http.Json;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using static PSPad.Api.Tests.Commands.Commands;

namespace PSPad.Api.Tests.Statistics;

public static class StatisticsFeed
{
    public static async Task<IReadOnlyList<StatisticsRecordView>> RecordsAsync(
        HttpClient client, CancellationToken ct, string query = "") =>
        await client.GetFromJsonAsync<StatisticsRecordView[]>($"/api/statistics/records{query}", ct) ?? [];

    public static async Task<StatisticsOverview> OverviewAsync(
        HttpClient client, CancellationToken ct, int days = 30) =>
        (await client.GetFromJsonAsync<StatisticsOverview>($"/api/statistics/overview?days={days}", ct))!;

    public static async Task<T> EventuallyAsync<T>(
        Func<Task<T>> read, Func<T, bool> until, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var value = await read();

            if (until(value))
            {
                return value;
            }

            await Task.Delay(100, ct);
        }

        throw new TimeoutException("the projection never caught up");
    }

    public static Task<IReadOnlyList<StatisticsRecordView>> FeedWhereAsync(
        HttpClient client, Func<IReadOnlyList<StatisticsRecordView>, bool> until, CancellationToken ct,
        string query = "") =>
        EventuallyAsync(() => RecordsAsync(client, ct, query), until, ct);

    public static async Task<Guid> ListAsync(HttpClient client, Guid user, CancellationToken ct, string name = "Errands")
    {
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0),
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, name));
        return listId;
    }
}
