using System.Text.Json;
using System.Text.Json.Nodes;
using PSPad.Abstractions;
using PSPad.Module.Tasks.Analytics;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Analytics;

[UnitTest]
public class BurndownRuleTests
{
    static readonly Guid User = Guid.NewGuid();
    static readonly Guid List = Guid.NewGuid();
    static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    static readonly DateOnly Today = new(2026, 3, 10);
    static readonly DateTimeOffset Now = new(2026, 3, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ItReturnsOnePointPerDayEndingToday()
    {
        var series = BurndownRule.Build([], Today, days: 5, Utc);

        Assert.Equal(5, series.Points.Count);
        Assert.Equal(new DateOnly(2026, 3, 6), series.Points[0].Day);
        Assert.Equal(Today, series.Points[^1].Day);
    }

    [Fact]
    public void ATaskIsOpenFromTheDayItWasCreated()
    {
        var task = Created(new DateTimeOffset(2026, 3, 8, 12, 0, 0, TimeSpan.Zero));

        var series = BurndownRule.Build([task], Today, days: 5, Utc);

        Assert.Equal(0, PointOn(series, new DateOnly(2026, 3, 7)).Open);
        Assert.Equal(1, PointOn(series, new DateOnly(2026, 3, 8)).Open);
        Assert.Equal(1, PointOn(series, Today).Open);
    }

    [Fact]
    public void ATaskStopsBeingOpenOnTheDayItWasCompleted()
    {
        var task = Created(new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero));
        Complete(task, new DateTimeOffset(2026, 3, 9, 15, 0, 0, TimeSpan.Zero));

        var series = BurndownRule.Build([task], Today, days: 5, Utc);

        Assert.Equal(1, PointOn(series, new DateOnly(2026, 3, 8)).Open);
        Assert.Equal(0, PointOn(series, new DateOnly(2026, 3, 9)).Open);
        Assert.Equal(1, PointOn(series, new DateOnly(2026, 3, 9)).Completed);
    }

    [Fact]
    public void ATaskCreatedAndCompletedOnTheSameDayIsClosedNotOpenAndCountsAsCompleted()
    {
        var sameDay = new DateTimeOffset(2026, 3, 8, 9, 0, 0, TimeSpan.Zero);
        var task = Created(sameDay);
        Complete(task, sameDay.AddHours(3));

        var series = BurndownRule.Build([task], Today, days: 5, Utc);

        Assert.Equal(0, PointOn(series, new DateOnly(2026, 3, 8)).Open);
        Assert.Equal(1, PointOn(series, new DateOnly(2026, 3, 8)).Completed);
    }

    [Fact]
    public void ARecurringTaskNeverJoinsTheOpenLine()
    {
        var task = Created(new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero));
        Repeat(task);

        var series = BurndownRule.Build([task], Today, days: 5, Utc);

        Assert.All(series.Points, point => Assert.Equal(0, point.Open));
    }

    [Fact]
    public void AnOccurrenceCountsAsACompletionOnItsOwnDay()
    {
        var task = Created(new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero));
        Repeat(task);
        TickOccurrence(task, new DateOnly(2026, 3, 9));

        var series = BurndownRule.Build([task], Today, days: 5, Utc);

        Assert.Equal(1, PointOn(series, new DateOnly(2026, 3, 9)).Completed);
        Assert.Equal(0, PointOn(series, new DateOnly(2026, 3, 8)).Completed);
    }

    [Fact]
    public void ADeletedTaskIsNotOpen()
    {
        var task = Created(new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero));
        Delete(task);

        var series = BurndownRule.Build([task], Today, days: 5, Utc);

        Assert.All(series.Points, point => Assert.Equal(0, point.Open));
    }

    [Fact]
    public void DaysAreBucketedInTheUsersZoneNotUtc()
    {
        var kiritimati = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Kiritimati");
        var task = Created(new DateTimeOffset(2026, 3, 7, 23, 0, 0, TimeSpan.Zero));

        var series = BurndownRule.Build([task], Today, days: 5, kiritimati);

        Assert.Equal(1, PointOn(series, new DateOnly(2026, 3, 8)).Open);
    }

    [Fact]
    public void ATaskWithNoCreatedAtIsOpenForTheWholeWindow()
    {
        var task = WithoutCreatedAt();

        var series = BurndownRule.Build([task], Today, days: 5, Utc);

        Assert.All(series.Points, point => Assert.Equal(1, point.Open));
    }

    static BurndownPoint PointOn(BurndownSeries series, DateOnly day) =>
        series.Points.Single(point => point.Day == day);

    static TodoTask Created(DateTimeOffset at)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), List, "Buy milk"), at));
        return task;
    }

    static void Complete(TodoTask task, DateTimeOffset at) =>
        task.ApplyAll(TodoTask.Decide(task, new CompleteTask(Guid.NewGuid(), User, task.Id), at));

    static void Repeat(TodoTask task) =>
        task.ApplyAll(TodoTask.Decide(
            task,
            new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, RecurrenceRule.Daily(new DateOnly(2026, 1, 1))),
            Now));

    static void TickOccurrence(TodoTask task, DateOnly day) =>
        task.ApplyAll(TodoTask.Decide(task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, day, true), Now));

    static void Delete(TodoTask task) =>
        task.ApplyAll(TodoTask.Decide(task, new DeleteTask(Guid.NewGuid(), User, task.Id), Now));

    static TodoTask WithoutCreatedAt()
    {
        var task = Created(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var json = JsonNode.Parse(JsonSerializer.Serialize(task, options))!.AsObject();
        json.Remove("createdAt");

        return json.Deserialize<TodoTask>(options)!;
    }
}
