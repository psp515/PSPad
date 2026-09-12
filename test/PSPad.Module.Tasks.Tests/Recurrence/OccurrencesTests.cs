using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Tests.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Recurrence;

[UnitTest]
public class OccurrencesTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void ADayInThePastWithNoCompletionIsSkipped()
    {
        var task = Daily();

        var occurrences = Occurrences.Between(task, Today.AddDays(-2), Today, Today);

        Assert.Equal(OccurrenceStatus.Skipped, occurrences[0].Status);
        Assert.Equal(OccurrenceStatus.Skipped, occurrences[1].Status);
    }

    [Fact]
    public void ACompletedDayIsDoneEvenInThePast()
    {
        var task = Daily();
        var yesterday = Today.AddDays(-1);
        task.ApplyAll(TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, yesterday, true), Now));

        var occurrences = Occurrences.Between(task, yesterday, yesterday, Today);

        Assert.Equal(OccurrenceStatus.Done, Assert.Single(occurrences).Status);
    }

    [Fact]
    public void TodayIsPendingUntilItIsTicked()
    {
        var task = Daily();

        var today = Assert.Single(Occurrences.Between(task, Today, Today, Today));

        Assert.Equal(OccurrenceStatus.Pending, today.Status);
    }

    [Fact]
    public void ADayInTheFutureIsPending()
    {
        var task = Daily();

        var tomorrow = Assert.Single(
            Occurrences.Between(task, Today.AddDays(1), Today.AddDays(1), Today));

        Assert.Equal(OccurrenceStatus.Pending, tomorrow.Status);
    }

    [Fact]
    public void DaysTheRuleDoesNotCoverAreNotOccurrencesAtAll()
    {
        var task = Weekly(DayOfWeek.Monday);

        var week = Occurrences.Between(task, new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13), Today);

        Assert.Equal([new DateOnly(2026, 9, 7)], week.Select(occurrence => occurrence.Day));
    }

    [Fact]
    public void APlainTaskHasNoOccurrences()
    {
        Assert.Empty(Occurrences.Between(TodoTaskTests.Existing(), Today.AddDays(-7), Today, Today));
    }

    static TodoTask Daily() => WithRule(RecurrenceRule.Daily(new DateOnly(2026, 9, 1)));

    static TodoTask Weekly(params DayOfWeek[] days) =>
        WithRule(RecurrenceRule.Weekly(new DateOnly(2026, 9, 1), days));

    static TodoTask WithRule(RecurrenceRule rule)
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, rule), Now));
        return task;
    }
}
