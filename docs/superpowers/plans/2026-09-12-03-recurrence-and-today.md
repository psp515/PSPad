# Recurrence and Today Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make a recurring task impossible to render overdue, and build the one
rule that decides what shows on Today.

**Architecture:** A recurrence rule plus the set of days a task was completed.
Pending and Skipped are derived from the rule and the current date, never stored.
The Today rule is a pure function over tasks, a date and a time zone, so every
edge it has can be tested without a clock, a database or a browser.

**Tech Stack:** .NET 10, xUnit v3, `TimeZoneInfo` from the base library.

**Spec:** `docs/superpowers/specs/2026-09-12-slice-1-design.md`

## Global Constraints

- Target framework `net10.0`, `Nullable` enable, `TreatWarningsAsErrors` true.
- No comments in code, except one line for a genuinely counter-intuitive constraint saying *why*.
- Every test class carries `[UnitTest]`. This plan has no integration tests.
- `PSPad.Module.Tasks` references `PSPad.Abstractions` and nothing else.
- "Today" means today in the user's IANA time zone. Machine-local time never appears; `DateTime.Now`, `DateTime.Today` and `DateTimeOffset.Now` are all banned in this module.
- A recurring task never becomes overdue. A missed day stays behind as skipped and never migrates forward.
- A star never puts a task on Today. Dates alone do that.
- Code, comments, commits and docs in English.

---

### Task 1: Recurrence rule

**Files:**
- Create: `src/modules/PSPad.Module.Tasks/Recurrence/RecurrenceRule.cs`
- Create: `src/modules/PSPad.Module.Tasks/Recurrence/RecurrenceKind.cs`
- Test: `test/PSPad.Module.Tasks.Tests/Recurrence/RecurrenceRuleTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `RecurrenceRule` with `RecurrenceKind Kind`, `DateOnly StartsOn`, `IReadOnlyList<DayOfWeek> Days`, `int DayOfMonth`, the factories `RecurrenceRule.Daily(DateOnly)`, `RecurrenceRule.Weekly(DateOnly, params DayOfWeek[])`, `RecurrenceRule.MonthlyOnDay(DateOnly, int)`, and `bool OccursOn(DateOnly day)`.

- [ ] **Step 1: Write the failing test**

```csharp
using PSPad.Abstractions;
using PSPad.Module.Tasks.Recurrence;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Recurrence;

[UnitTest]
public class RecurrenceRuleTests
{
    static readonly DateOnly Start = new(2026, 9, 7);

    [Fact]
    public void ADailyRuleOccursEveryDayFromItsStart()
    {
        var rule = RecurrenceRule.Daily(Start);

        Assert.True(rule.OccursOn(Start));
        Assert.True(rule.OccursOn(Start.AddDays(1)));
        Assert.False(rule.OccursOn(Start.AddDays(-1)));
    }

    [Fact]
    public void AWeeklyRuleOccursOnlyOnItsDays()
    {
        var rule = RecurrenceRule.Weekly(Start, DayOfWeek.Monday, DayOfWeek.Thursday);

        Assert.True(rule.OccursOn(new DateOnly(2026, 9, 7)));
        Assert.True(rule.OccursOn(new DateOnly(2026, 9, 10)));
        Assert.False(rule.OccursOn(new DateOnly(2026, 9, 8)));
    }

    [Fact]
    public void AWeeklyRuleWithNoDaysIsRejected()
    {
        Assert.Throws<DomainRejectedException>(() => RecurrenceRule.Weekly(Start));
    }

    [Fact]
    public void AMonthlyRuleOccursOnItsDayOfTheMonth()
    {
        var rule = RecurrenceRule.MonthlyOnDay(Start, 15);

        Assert.True(rule.OccursOn(new DateOnly(2026, 9, 15)));
        Assert.True(rule.OccursOn(new DateOnly(2026, 10, 15)));
        Assert.False(rule.OccursOn(new DateOnly(2026, 9, 16)));
    }

    [Fact]
    public void AMonthlyRuleOnADayThatMonthLacksFallsOnTheLastDay()
    {
        var rule = RecurrenceRule.MonthlyOnDay(Start, 31);

        Assert.True(rule.OccursOn(new DateOnly(2026, 11, 30)));
        Assert.False(rule.OccursOn(new DateOnly(2026, 11, 29)));
    }

    [Fact]
    public void AMonthlyDayOutsideOneToThirtyOneIsRejected()
    {
        Assert.Throws<DomainRejectedException>(() => RecurrenceRule.MonthlyOnDay(Start, 0));
        Assert.Throws<DomainRejectedException>(() => RecurrenceRule.MonthlyOnDay(Start, 32));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.Module.Tasks.Tests --filter Category=Unit`
Expected: compile error — `RecurrenceRule` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
namespace PSPad.Module.Tasks.Recurrence;

public enum RecurrenceKind
{
    Daily = 0,
    Weekly = 1,
    MonthlyOnDay = 2
}
```

```csharp
using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Recurrence;

public sealed record RecurrenceRule(
    RecurrenceKind Kind,
    DateOnly StartsOn,
    IReadOnlyList<DayOfWeek> Days,
    int DayOfMonth)
{
    public static RecurrenceRule Daily(DateOnly startsOn) =>
        new(RecurrenceKind.Daily, startsOn, [], 0);

    public static RecurrenceRule Weekly(DateOnly startsOn, params DayOfWeek[] days) =>
        days.Length == 0
            ? throw new DomainRejectedException("A weekly repeat needs at least one day.")
            : new RecurrenceRule(RecurrenceKind.Weekly, startsOn, days.Distinct().ToArray(), 0);

    public static RecurrenceRule MonthlyOnDay(DateOnly startsOn, int dayOfMonth) =>
        dayOfMonth is < 1 or > 31
            ? throw new DomainRejectedException("A monthly repeat needs a day between 1 and 31.")
            : new RecurrenceRule(RecurrenceKind.MonthlyOnDay, startsOn, [], dayOfMonth);

    public bool OccursOn(DateOnly day)
    {
        if (day < StartsOn)
        {
            return false;
        }

        return Kind switch
        {
            RecurrenceKind.Daily => true,
            RecurrenceKind.Weekly => Days.Contains(day.DayOfWeek),
            RecurrenceKind.MonthlyOnDay => day.Day == EffectiveDayIn(day.Year, day.Month),
            _ => false
        };
    }

    int EffectiveDayIn(int year, int month) =>
        Math.Min(DayOfMonth, DateTime.DaysInMonth(year, month));
}
```

A "31st of the month" rule has to land somewhere in November, and skipping the
month outright would silently drop an occurrence the user expects to see.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test test/PSPad.Module.Tasks.Tests --filter Category=Unit`
Expected: PASS, six tests.

- [ ] **Step 5: Commit**

```bash
git add src test
git commit -m "feat: model recurrence as a rule over days"
```

---

### Task 2: Recurrence on a task

**Files:**
- Modify: `src/modules/PSPad.Module.Tasks/Tasks/TodoTask.cs`
- Modify: `src/modules/PSPad.Module.Tasks/Tasks/TodoTaskCommands.cs`
- Modify: `src/modules/PSPad.Module.Tasks/Tasks/TodoTaskEvents.cs`
- Modify: `src/modules/PSPad.Module.Tasks/Tasks/TodoTaskHandlers.cs`
- Test: `test/PSPad.Module.Tasks.Tests/Tasks/TaskRecurrenceTests.cs`

**Interfaces:**
- Consumes: `TodoTask` from plan 02, `RecurrenceRule` from Task 1.
- Produces: `TodoTask.Recurrence` (`RecurrenceRule?`), `TodoTask.CompletedDays` (`IReadOnlySet<DateOnly>`), `TodoTask.IsRecurring`; commands `SetTaskRecurrence(CommandId, UserId, TaskId, RecurrenceRule?)` and `CompleteOccurrence(CommandId, UserId, TaskId, DateOnly Day, bool Completed)`; events `TaskRecurrenceSet` and `OccurrenceCompleted`.

- [ ] **Step 1: Write the failing test**

```csharp
using PSPad.Abstractions;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Tasks;

[UnitTest]
public class TaskRecurrenceTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void ATaskWithARuleIsRecurring()
    {
        var task = Recurring();

        Assert.True(task.IsRecurring);
        Assert.NotNull(task.Recurrence);
    }

    [Fact]
    public void ARecurringTaskCannotCarryADueDate()
    {
        var task = Recurring();

        Assert.Throws<DomainRejectedException>(() => TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, Today), Now));
    }

    [Fact]
    public void CompletingAnOccurrenceRecordsThatDayOnly()
    {
        var task = Recurring();

        task.ApplyAll(TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, Today, true), Now));

        Assert.Contains(Today, task.CompletedDays);
        Assert.DoesNotContain(Today.AddDays(-1), task.CompletedDays);
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void UncompletingAnOccurrenceTakesTheDayBackOut()
    {
        var task = Recurring();
        task.ApplyAll(TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, Today, true), Now));

        task.ApplyAll(TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, Today, false), Now));

        Assert.Empty(task.CompletedDays);
    }

    [Fact]
    public void CompletingADayTheRuleDoesNotCoverIsRejected()
    {
        var task = Recurring(RecurrenceRule.Weekly(new DateOnly(2026, 9, 7), DayOfWeek.Monday));

        Assert.Throws<DomainRejectedException>(() => TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, new DateOnly(2026, 9, 12), true), Now));
    }

    [Fact]
    public void CompletingTheWholeTaskIsRejectedWhileItRecurs()
    {
        var task = Recurring();

        Assert.Throws<DomainRejectedException>(() => TodoTask.Decide(
            task, new CompleteTask(Guid.NewGuid(), User, task.Id), Now));
    }

    [Fact]
    public void ClearingTheRuleLeavesAPlainTask()
    {
        var task = Recurring();

        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, null), Now));

        Assert.False(task.IsRecurring);
    }

    static TodoTask Recurring(RecurrenceRule? rule = null)
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(
            task,
            new SetTaskRecurrence(
                Guid.NewGuid(), User, task.Id, rule ?? RecurrenceRule.Daily(new DateOnly(2026, 9, 1))),
            Now));
        return task;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.Module.Tasks.Tests --filter Category=Unit`
Expected: compile error — `SetTaskRecurrence` does not exist.

- [ ] **Step 3: Add the commands and events**

```csharp
public sealed record SetTaskRecurrence(Guid CommandId, Guid UserId, Guid TaskId, RecurrenceRule? Rule)
    : ICommand;

public sealed record CompleteOccurrence(
    Guid CommandId, Guid UserId, Guid TaskId, DateOnly Day, bool Completed) : ICommand;
```

```csharp
public sealed record TaskRecurrenceSet(
    Guid AggregateId, Guid UserId, DateTimeOffset At, RecurrenceRule? Rule)
    : DomainEvent(AggregateId, UserId, At);

public sealed record OccurrenceCompleted(
    Guid AggregateId, Guid UserId, DateTimeOffset At, DateOnly Day, bool Completed)
    : DomainEvent(AggregateId, UserId, At);
```

- [ ] **Step 4: Extend the aggregate**

Add to `TodoTask`:

```csharp
    readonly HashSet<DateOnly> _completedDays = [];

    public RecurrenceRule? Recurrence { get; private set; }

    public IReadOnlySet<DateOnly> CompletedDays => _completedDays;

    public bool IsRecurring => Recurrence is not null;
```

New `Decide` arms:

```csharp
            case SetTaskRecurrence recurrence:
                var repeating = Require(task, recurrence.UserId);
                if (recurrence.Rule is not null && repeating.DueOn is not null)
                {
                    throw new DomainRejectedException("A repeating task cannot also have a due date.");
                }

                return repeating.Recurrence == recurrence.Rule
                    ? []
                    : [new TaskRecurrenceSet(repeating.Id, recurrence.UserId, at, recurrence.Rule)];

            case CompleteOccurrence occurrence:
                var ticking = Require(task, occurrence.UserId);
                if (ticking.Recurrence is null)
                {
                    throw new DomainRejectedException("That task does not repeat.");
                }

                if (!ticking.Recurrence.OccursOn(occurrence.Day))
                {
                    throw new DomainRejectedException("That task does not repeat on that day.");
                }

                return ticking.CompletedDays.Contains(occurrence.Day) == occurrence.Completed
                    ? []
                    : [new OccurrenceCompleted(
                        ticking.Id, occurrence.UserId, at, occurrence.Day, occurrence.Completed)];
```

Guard the two existing arms:

```csharp
            case SetTaskDueDate due:
                var dating = Require(task, due.UserId);
                if (due.DueOn is not null && dating.IsRecurring)
                {
                    throw new DomainRejectedException("A repeating task cannot also have a due date.");
                }
                ...

            case CompleteTask complete:
                var completing = Require(task, complete.UserId);
                if (completing.IsRecurring)
                {
                    throw new DomainRejectedException("Tick today's occurrence instead of the whole task.");
                }
                ...
```

That last rejection is the whole never-overdue rule in one place: a repeating
task has no single completion, so there is nothing for an overdue state to
attach to.

New `When` arms:

```csharp
            case TaskRecurrenceSet recurrenceSet:
                Recurrence = recurrenceSet.Rule;
                if (recurrenceSet.Rule is null)
                {
                    _completedDays.Clear();
                }

                break;
            case OccurrenceCompleted occurrenceCompleted:
                if (occurrenceCompleted.Completed)
                {
                    _completedDays.Add(occurrenceCompleted.Day);
                }
                else
                {
                    _completedDays.Remove(occurrenceCompleted.Day);
                }

                break;
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test test/PSPad.Module.Tasks.Tests --filter Category=Unit`
Expected: PASS, seven tests in `TaskRecurrenceTests`.

- [ ] **Step 6: Add the two handlers**

`SetTaskRecurrenceHandler` and `CompleteOccurrenceHandler` in
`TodoTaskHandlers.cs`, same shape as the rest.

- [ ] **Step 7: Commit**

```bash
git add src test
git commit -m "feat: repeat a task by rule and tick single occurrences"
```

---

### Task 3: Derived occurrences

**Files:**
- Create: `src/modules/PSPad.Module.Tasks/Recurrence/OccurrenceStatus.cs`
- Create: `src/modules/PSPad.Module.Tasks/Recurrence/Occurrence.cs`
- Create: `src/modules/PSPad.Module.Tasks/Recurrence/Occurrences.cs`
- Test: `test/PSPad.Module.Tasks.Tests/Recurrence/OccurrencesTests.cs`

**Interfaces:**
- Consumes: `RecurrenceRule` from Task 1, `TodoTask` from Task 2.
- Produces: `OccurrenceStatus { Done, Pending, Skipped }`, `Occurrence(DateOnly Day, OccurrenceStatus Status)`, and `Occurrences.Between(TodoTask task, DateOnly from, DateOnly to, DateOnly today)` returning `IReadOnlyList<Occurrence>`. Plan 06's history screen and plan 07's task detail both read this.

Only completed days are stored. Everything else is computed here, which is what
keeps a missed day from ever turning into an overdue one.

- [ ] **Step 1: Write the failing test**

```csharp
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.Module.Tasks.Tests --filter Category=Unit`
Expected: compile error — `Occurrences` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
namespace PSPad.Module.Tasks.Recurrence;

public enum OccurrenceStatus
{
    Done = 0,
    Pending = 1,
    Skipped = 2
}
```

```csharp
namespace PSPad.Module.Tasks.Recurrence;

public sealed record Occurrence(DateOnly Day, OccurrenceStatus Status);
```

```csharp
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Tasks.Recurrence;

public static class Occurrences
{
    public static IReadOnlyList<Occurrence> Between(TodoTask task, DateOnly from, DateOnly to, DateOnly today)
    {
        if (task.Recurrence is null)
        {
            return [];
        }

        var occurrences = new List<Occurrence>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (task.Recurrence.OccursOn(day))
            {
                occurrences.Add(new Occurrence(day, StatusOf(task, day, today)));
            }
        }

        return occurrences;
    }

    static OccurrenceStatus StatusOf(TodoTask task, DateOnly day, DateOnly today)
    {
        if (task.CompletedDays.Contains(day))
        {
            return OccurrenceStatus.Done;
        }

        return day < today ? OccurrenceStatus.Skipped : OccurrenceStatus.Pending;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test test/PSPad.Module.Tasks.Tests --filter Category=Unit`
Expected: PASS, six tests.

- [ ] **Step 5: Commit**

```bash
git add src test
git commit -m "feat: derive occurrence status from the rule and today"
```

---

### Task 4: The Today rule

**Files:**
- Create: `src/modules/PSPad.Module.Tasks/Today/TodayEntry.cs`
- Create: `src/modules/PSPad.Module.Tasks/Today/TodayRule.cs`
- Test: `test/PSPad.Module.Tasks.Tests/Today/TodayRuleTests.cs`

**Interfaces:**
- Consumes: `TodoTask` with steps and recurrence.
- Produces: `TodayRule.TodayIn(DateTimeOffset instant, TimeZoneInfo zone)` returning `DateOnly`, and `TodayRule.Select(IEnumerable<TodoTask> tasks, DateOnly today)` returning `IReadOnlyList<TodayEntry>` sorted overdue first. Plan 04's `/api/today` and plan 07's Today screen both call exactly these two.

- [ ] **Step 1: Write the failing test**

```csharp
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Today;

[UnitTest]
public class TodayRuleTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void TodayIsReadInTheUsersZoneNotUtc()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");
        var lateUtc = new DateTimeOffset(2026, 9, 12, 23, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 9, 13), TodayRule.TodayIn(lateUtc, zone));
    }

    [Fact]
    public void ATaskDueTodayIsOnToday()
    {
        var task = Due(Today);

        var entry = Assert.Single(TodayRule.Select([task], Today));

        Assert.Equal(task.Id, entry.TaskId);
        Assert.False(entry.Overdue);
    }

    [Fact]
    public void ATaskDueEarlierIsOverdue()
    {
        var entry = Assert.Single(TodayRule.Select([Due(Today.AddDays(-3))], Today));

        Assert.True(entry.Overdue);
    }

    [Fact]
    public void ATaskDueLaterIsNotOnToday()
    {
        Assert.Empty(TodayRule.Select([Due(Today.AddDays(1))], Today));
    }

    [Fact]
    public void ATaskWhoseNextUncheckedStepIsDueTodayIsOnToday()
    {
        var task = TodoTaskTests.Existing();
        var stepId = Guid.NewGuid();
        task.ApplyAll(TodoTask.Decide(task, new AddStep(Guid.NewGuid(), User, task.Id, stepId, "call"), Now));
        task.ApplyAll(TodoTask.Decide(
            task, new SetStepDueDate(Guid.NewGuid(), User, task.Id, stepId, Today), Now));

        Assert.Single(TodayRule.Select([task], Today));
    }

    [Fact]
    public void ACheckedStepStopsDrivingToday()
    {
        var task = TodoTaskTests.Existing();
        var stepId = Guid.NewGuid();
        task.ApplyAll(TodoTask.Decide(task, new AddStep(Guid.NewGuid(), User, task.Id, stepId, "call"), Now));
        task.ApplyAll(TodoTask.Decide(
            task, new SetStepDueDate(Guid.NewGuid(), User, task.Id, stepId, Today), Now));
        task.ApplyAll(TodoTask.Decide(
            task, new CheckStep(Guid.NewGuid(), User, task.Id, stepId, true), Now));

        Assert.Empty(TodayRule.Select([task], Today));
    }

    [Fact]
    public void AStarNeverPutsATaskOnToday()
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(task, new StarTask(Guid.NewGuid(), User, task.Id, true), Now));

        Assert.Empty(TodayRule.Select([task], Today));
    }

    [Fact]
    public void ACompletedTaskDropsOffToday()
    {
        var task = Due(Today);
        task.ApplyAll(TodoTask.Decide(task, new CompleteTask(Guid.NewGuid(), User, task.Id), Now));

        Assert.Empty(TodayRule.Select([task], Today));
    }

    [Fact]
    public void ARecurringTaskShowsOnlyTodaysPendingOccurrence()
    {
        var task = DailyFrom(Today.AddDays(-5));

        var entry = Assert.Single(TodayRule.Select([task], Today));

        Assert.False(entry.Overdue);
    }

    [Fact]
    public void ARecurringTaskMissedYesterdayIsStillNotOverdueToday()
    {
        var task = DailyFrom(Today.AddDays(-5));

        var entry = Assert.Single(TodayRule.Select([task], Today));

        Assert.False(entry.Overdue);
        Assert.Equal(OccurrenceStatus.Skipped,
            Occurrences.Between(task, Today.AddDays(-1), Today.AddDays(-1), Today).Single().Status);
    }

    [Fact]
    public void ARecurringTaskTickedTodayDropsOffToday()
    {
        var task = DailyFrom(Today.AddDays(-5));
        task.ApplyAll(TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, Today, true), Now));

        Assert.Empty(TodayRule.Select([task], Today));
    }

    [Fact]
    public void ARecurringTaskThatDoesNotFallTodayIsNotOnToday()
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(
            task,
            new SetTaskRecurrence(
                Guid.NewGuid(), User, task.Id,
                RecurrenceRule.Weekly(Today.AddDays(-7), DayOfWeek.Monday)),
            Now));

        Assert.Empty(TodayRule.Select([task], Today));
    }

    [Fact]
    public void OverdueTasksSortAboveTheRest()
    {
        var overdue = Due(Today.AddDays(-1));
        var dueToday = Due(Today);

        var entries = TodayRule.Select([dueToday, overdue], Today);

        Assert.Equal(overdue.Id, entries[0].TaskId);
        Assert.Equal(dueToday.Id, entries[1].TaskId);
    }

    [Fact]
    public void WithinTheSameUrgencyStarsThenPrioritySortUp()
    {
        var plain = Due(Today);
        var important = Due(Today);
        important.ApplyAll(TodoTask.Decide(
            important, new StarTask(Guid.NewGuid(), User, important.Id, true), Now));
        var urgent = Due(Today);
        urgent.ApplyAll(TodoTask.Decide(
            urgent, new SetTaskPriority(Guid.NewGuid(), User, urgent.Id, Priority.High), Now));

        var entries = TodayRule.Select([plain, urgent, important], Today);

        Assert.Equal(important.Id, entries[0].TaskId);
        Assert.Equal(urgent.Id, entries[1].TaskId);
        Assert.Equal(plain.Id, entries[2].TaskId);
    }

    static TodoTask Due(DateOnly day)
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, day), Now));
        return task;
    }

    static TodoTask DailyFrom(DateOnly start)
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, RecurrenceRule.Daily(start)), Now));
        return task;
    }
}
```

`TodoTaskTests.Existing()` is `internal static` and every task it builds gets a
fresh id, so the sort assertions above compare distinct tasks.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.Module.Tasks.Tests --filter Category=Unit`
Expected: compile error — `TodayRule` does not exist.

- [ ] **Step 3: Write the entry**

```csharp
namespace PSPad.Module.Tasks.Today;

public sealed record TodayEntry(
    Guid TaskId,
    Guid ListId,
    string Name,
    bool Overdue,
    DateOnly? DueOn,
    bool Recurring,
    bool Starred,
    int Priority);
```

- [ ] **Step 4: Write the rule**

```csharp
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Tasks.Today;

public static class TodayRule
{
    public static DateOnly TodayIn(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).Date);

    public static IReadOnlyList<TodayEntry> Select(IEnumerable<TodoTask> tasks, DateOnly today) =>
        tasks
            .Where(task => !task.Deleted)
            .Select(task => Consider(task, today))
            .OfType<TodayEntry>()
            .OrderByDescending(entry => entry.Overdue)
            .ThenByDescending(entry => entry.Starred)
            .ThenByDescending(entry => entry.Priority)
            .ThenBy(entry => entry.DueOn ?? today)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    static TodayEntry? Consider(TodoTask task, DateOnly today)
    {
        if (task.IsRecurring)
        {
            var due = task.Recurrence!.OccursOn(today) && !task.CompletedDays.Contains(today);
            return due ? Entry(task, overdue: false, dueOn: today) : null;
        }

        if (task.CompletedAt is not null)
        {
            return null;
        }

        var trigger = EarliestTrigger(task);
        if (trigger is null || trigger > today)
        {
            return null;
        }

        return Entry(task, overdue: trigger < today, dueOn: trigger);
    }

    static DateOnly? EarliestTrigger(TodoTask task)
    {
        var stepDue = task.NextUncheckedStep?.DueOn;

        return (task.DueOn, stepDue) switch
        {
            (null, null) => null,
            (var due, null) => due,
            (null, var step) => step,
            var (due, step) => due <= step ? due : step
        };
    }

    static TodayEntry Entry(TodoTask task, bool overdue, DateOnly? dueOn) =>
        new(task.Id, task.ListId, task.Name, overdue, dueOn, task.IsRecurring,
            task.Starred, (int)task.Priority);
}
```

A recurring task is never handed `overdue: true`. That is not an oversight to be
tidied up later — it is the rule.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test test/PSPad.Module.Tasks.Tests --filter Category=Unit`
Expected: PASS, fourteen tests in `TodayRuleTests`.

- [ ] **Step 6: Ban machine-local time in the module**

Add to `test/PSPad.Module.Tasks.Tests/ArchitectureTests.cs`:

```csharp
    [Fact]
    public void TheModuleNeverReadsMachineLocalTime()
    {
        var source = Directory.EnumerateFiles(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "modules", "PSPad.Module.Tasks"),
            "*.cs", SearchOption.AllDirectories);

        var offenders = source
            .Where(file => File.ReadAllText(file) is var text &&
                (text.Contains("DateTime.Now") || text.Contains("DateTime.Today") ||
                 text.Contains("DateTimeOffset.Now")))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.True(offenders.Length == 0, $"Machine-local time in: {string.Join(", ", offenders)}");
    }
```

Run: `dotnet test test/PSPad.Module.Tasks.Tests --filter Category=Unit`
Expected: PASS. If the path resolution fails, print the computed directory once
and correct the number of `..` segments for the actual output layout.

- [ ] **Step 7: Commit**

```bash
git add src test
git commit -m "feat: decide Today in the user's own time zone"
```

---

## Done when

`dotnet test --filter Category=Unit` passes, a daily task missed yesterday
reports `Skipped` for yesterday and a non-overdue entry for today, and nothing in
`PSPad.Module.Tasks` reads machine-local time.
