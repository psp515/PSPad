using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class TodayEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task ATaskDueYesterdayComesBackOverdue()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        var taskId = await SeedTask(client, user);
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-1);
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new SetTaskDueDate(Guid.NewGuid(), user, taskId, yesterday))
        }, ct);

        var today = await client.GetFromJsonAsync<TodayEntry[]>("/api/today", ct);

        Assert.True(Assert.Single(today!).Overdue);
    }

    [Fact]
    public async Task ARecurringTaskMissedYesterdayIsNeverOverdue()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        var taskId = await SeedTask(client, user);
        var lastWeek = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-7);
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new SetTaskRecurrence(
                Guid.NewGuid(), user, taskId, RecurrenceRule.Daily(lastWeek)))
        }, ct);

        var today = await client.GetFromJsonAsync<TodayEntry[]>("/api/today", ct);

        var entry = Assert.Single(today!);
        Assert.False(entry.Overdue);
        Assert.True(entry.Recurring);
    }

    [Fact]
    public async Task TodayIsReadInTheZoneTheUserHasSet()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString(), "Pacific/Auckland");
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        Assert.Equal("Pacific/Auckland", me.TimeZone);
        var taskId = await SeedTask(client, user);
        var aucklandToday = TodayRule.TodayIn(
            DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland"));
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new SetTaskDueDate(Guid.NewGuid(), user, taskId, aucklandToday))
        }, ct);

        var today = await client.GetFromJsonAsync<TodayEntry[]>("/api/today", ct);

        Assert.False(Assert.Single(today!).Overdue);
    }

    static async Task<Guid> SeedTask(HttpClient client, Guid user)
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0)),
            Envelope(new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Errands", 0)),
            Envelope(new CreateTask(Guid.NewGuid(), user, taskId, listId, "Buy milk"))
        }, ct);

        return taskId;
    }

    static CommandEnvelope Envelope<T>(T command) where T : notnull =>
        new(typeof(T).Name, JsonSerializer.SerializeToElement(command));
}
