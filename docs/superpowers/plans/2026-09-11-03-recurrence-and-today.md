# Recurrence & Today Rule Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Recurring tasks that generate per-day occurrences and never accumulate overdue debt, plus the single pure function that decides what appears on Today.

**Architecture:** A recurrence rule is a pure predicate over `DateOnly`. Occurrences are created lazily — one record per scheduled day the user actually reaches — and a missed day is *derived* as skipped rather than stored. The Today rule is one static function taking a task snapshot and the user's local today, so the client and the server compute the same screen from the same code (spec §5, §6).

**Tech Stack:** .NET 10, C# records, xUnit, Shouldly

**Spec:** `docs/superpowers/specs/2026-09-11-gtd-core-design.md`

## Global Constraints

- .NET 10 (LTS). All projects target `net10.0`.
- `PSPad.Domain` references no infrastructure packages and must compile for WebAssembly.
- All user-facing dates are `DateOnly` in the user's time zone; all stored instants are UTC `DateTimeOffset`.
- Decide functions never read the clock — "today" is always a parameter.
- Code, comments, commits and docs in English.
- GPL v3 — every dependency must be license-compatible.

**Depends on:** plan 02 (task aggregate, `RecurrenceRule` placeholder).

---

### Task 1: Recurrence rule as a pure predicate

**Files:**
- Modify: `src/PSPad.Domain/Tasks/RecurrenceRule.cs` (replace the placeholder)
- Test: `test/PSPad.Domain.Tests/Tasks/RecurrenceRuleTests.cs`

**Interfaces:**
- Consumes: `DomainException`, `ErrorCodes` from plan 02 Task 1
- Produces: `enum RecurrenceKind { Daily, DaysOfWeek, EveryNDays }`; `RecurrenceRule(RecurrenceKind Kind, int Interval, IReadOnlyList<DayOfWeek> DaysOfWeek, DateOnly StartDate, DateOnly? EndDate)` with `bool Occurs(DateOnly day)`, `void Validate()`, and the factories `RecurrenceRule.Daily(DateOnly start, DateOnly? end = null)`, `RecurrenceRule.OnDays(DateOnly start, params DayOfWeek[] days)`, `RecurrenceRule.EveryNDays(DateOnly start, int interval)`

- [ ] **Step 1: Write the failing test**

`test/PSPad.Domain.Tests/Tasks/RecurrenceRuleTests.cs`:

```csharp
using PSPad.Domain.Common;
using PSPad.Domain.Tasks;
using Shouldly;

namespace PSPad.Domain.Tests.Tasks;

public class RecurrenceRuleTests
{
    static readonly DateOnly Friday = new(2026, 9, 11);
    static readonly DateOnly Saturday = new(2026, 9, 12);
    static readonly DateOnly Monday = new(2026, 9, 14);

    [Fact]
    public void Daily_occurs_on_every_day_from_the_start()
    {
        var rule = RecurrenceRule.Daily(Friday);

        rule.Occurs(Friday).ShouldBeTrue();
        rule.Occurs(Saturday).ShouldBeTrue();
        rule.Occurs(Monday).ShouldBeTrue();
    }

    [Fact]
    public void Nothing_occurs_before_the_start_date()
    {
        RecurrenceRule.Daily(Saturday).Occurs(Friday).ShouldBeFalse();
    }

    [Fact]
    public void Nothing_occurs_after_the_end_date()
    {
        var rule = RecurrenceRule.Daily(Friday, Saturday);

        rule.Occurs(Saturday).ShouldBeTrue();
        rule.Occurs(Monday).ShouldBeFalse();
    }

    [Fact]
    public void Days_of_week_occurs_only_on_the_listed_days()
    {
        var rule = RecurrenceRule.OnDays(Friday, DayOfWeek.Monday, DayOfWeek.Friday);

        rule.Occurs(Friday).ShouldBeTrue();
        rule.Occurs(Saturday).ShouldBeFalse();
        rule.Occurs(Monday).ShouldBeTrue();
    }

    [Fact]
    public void Every_n_days_counts_from_the_start_date()
    {
        var rule = RecurrenceRule.EveryNDays(Friday, interval: 3);

        rule.Occurs(Friday).ShouldBeTrue();
        rule.Occurs(Friday.AddDays(1)).ShouldBeFalse();
        rule.Occurs(Friday.AddDays(3)).ShouldBeTrue();
        rule.Occurs(Friday.AddDays(6)).ShouldBeTrue();
    }

    [Fact]
    public void An_interval_below_one_is_rejected()
    {
        Should.Throw<DomainException>(() => RecurrenceRule.EveryNDays(Friday, interval: 0).Validate())
            .Code.ShouldBe(ErrorCodes.InvalidRecurrence);
    }

    [Fact]
    public void An_empty_day_list_is_rejected()
    {
        Should.Throw<DomainException>(() => RecurrenceRule.OnDays(Friday).Validate())
            .Code.ShouldBe(ErrorCodes.InvalidRecurrence);
    }

    [Fact]
    public void An_end_before_the_start_is_rejected()
    {
        var rule = RecurrenceRule.Daily(Monday, Friday);

        Should.Throw<DomainException>(rule.Validate).Code.ShouldBe(ErrorCodes.InvalidRecurrence);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test test/PSPad.Domain.Tests --filter RecurrenceRuleTests`
Expected: FAIL — `RecurrenceRule.Daily` does not exist (the placeholder has no members).

- [ ] **Step 3: Replace the placeholder with the real rule**

`src/PSPad.Domain/Tasks/RecurrenceRule.cs`:

```csharp
using PSPad.Domain.Common;

namespace PSPad.Domain.Tasks;

public enum RecurrenceKind
{
    Daily = 0,
    DaysOfWeek = 1,
    EveryNDays = 2
}

/// <summary>
/// The single source of truth for whether a day is scheduled (spec §6).
/// Pure: no clock, no storage. Occurrences are derived from this, never the
/// other way around.
/// </summary>
public sealed record RecurrenceRule(
    RecurrenceKind Kind,
    int Interval,
    IReadOnlyList<DayOfWeek> DaysOfWeek,
    DateOnly StartDate,
    DateOnly? EndDate)
{
    public static RecurrenceRule Daily(DateOnly start, DateOnly? end = null) =>
        new(RecurrenceKind.Daily, Interval: 1, DaysOfWeek: [], start, end);

    public static RecurrenceRule OnDays(DateOnly start, params DayOfWeek[] days) =>
        new(RecurrenceKind.DaysOfWeek, Interval: 1, days, start, EndDate: null);

    public static RecurrenceRule EveryNDays(DateOnly start, int interval) =>
        new(RecurrenceKind.EveryNDays, interval, DaysOfWeek: [], start, EndDate: null);

    public bool Occurs(DateOnly day)
    {
        if (day < StartDate)
            return false;

        if (EndDate is { } end && day > end)
            return false;

        return Kind switch
        {
            RecurrenceKind.Daily => true,
            RecurrenceKind.DaysOfWeek => DaysOfWeek.Contains(day.DayOfWeek),
            RecurrenceKind.EveryNDays => (day.DayNumber - StartDate.DayNumber) % Interval == 0,
            _ => false
        };
    }

    public void Validate()
    {
        if (EndDate is { } end && end < StartDate)
            throw new DomainException(
                ErrorCodes.InvalidRecurrence, "The end date cannot precede the start date.");

        switch (Kind)
        {
            case RecurrenceKind.EveryNDays when Interval < 1:
                throw new DomainException(
                    ErrorCodes.InvalidRecurrence, "The interval must be at least one day.");

            case RecurrenceKind.DaysOfWeek when DaysOfWeek.Count == 0:
                throw new DomainException(
                    ErrorCodes.InvalidRecurrence, "At least one day of the week is required.");
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test test/PSPad.Domain.Tests --filter RecurrenceRuleTests`
Expected: PASS, 8 tests.

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.Domain/Tasks/RecurrenceRule.cs test/PSPad.Domain.Tests/Tasks/RecurrenceRuleTests.cs
git commit -m "feat(domain): add recurrence rule as a pure day predicate"
```

---

### Task 2: Setting and clearing recurrence on a task

**Files:**
- Modify: `src/PSPad.Domain/Tasks/TaskCommands.cs`
- Modify: `src/PSPad.Domain/Tasks/TaskEvents.cs`
- Modify: `src/PSPad.Domain/Tasks/TodoTaskDecider.cs`
- Test: `test/PSPad.Domain.Tests/Tasks/TaskRecurrenceTests.cs`

**Interfaces:**
- Consumes: Task 1, plan 02 Task 5
- Produces: commands `SetRecurrence(..., TaskId, RecurrenceRule Rule)`, `ClearRecurrence(..., TaskId)`; events `RecurrenceSet(..., TaskId, RecurrenceRule Rule)`, `RecurrenceCleared(..., TaskId)`; the invariant that a recurring task holds no due date (spec §3.3)

- [ ] **Step 1: Write the failing test**

`test/PSPad.Domain.Tests/Tasks/TaskRecurrenceTests.cs`:

```csharp
using PSPad.Domain.Common;
using PSPad.Domain.Tasks;
using Shouldly;

namespace PSPad.Domain.Tests.Tasks;

public class TaskRecurrenceTests
{
    static readonly UserId User = UserId.New();
    static readonly ListId List = ListId.New();
    static readonly TaskId Task = TaskId.New();
    static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 9, 11);

    static TodoTaskState Plain(DateOnly? dueDate = null) => new(
        Task, User, List, "Read a book", dueDate, Priority.None,
        false, null, [], null, Recurrence: null);

    [Fact]
    public void Setting_recurrence_clears_an_existing_due_date()
    {
        var command = new SetRecurrence(
            User, Guid.NewGuid(), Now, Task, RecurrenceRule.Daily(Today));

        var events = TodoTaskDecider.Decide(Plain(dueDate: Today), command);

        events.Count.ShouldBe(2);
        events[0].ShouldBeOfType<TaskDueDateSet>().DueDate.ShouldBeNull();
        events[1].ShouldBeOfType<RecurrenceSet>().Rule.Kind.ShouldBe(RecurrenceKind.Daily);
    }

    [Fact]
    public void Setting_recurrence_on_an_undated_task_emits_one_event()
    {
        var command = new SetRecurrence(
            User, Guid.NewGuid(), Now, Task, RecurrenceRule.Daily(Today));

        TodoTaskDecider.Decide(Plain(), command)
            .ShouldHaveSingleItem().ShouldBeOfType<RecurrenceSet>();
    }

    [Fact]
    public void An_invalid_rule_is_rejected()
    {
        var command = new SetRecurrence(
            User, Guid.NewGuid(), Now, Task, RecurrenceRule.EveryNDays(Today, interval: 0));

        Should.Throw<DomainException>(() => TodoTaskDecider.Decide(Plain(), command))
            .Code.ShouldBe(ErrorCodes.InvalidRecurrence);
    }

    [Fact]
    public void Giving_a_recurring_task_a_due_date_is_rejected()
    {
        var state = Plain() with { Recurrence = RecurrenceRule.Daily(Today) };
        var command = new SetTaskDueDate(User, Guid.NewGuid(), Now, Task, Today);

        Should.Throw<DomainException>(() => TodoTaskDecider.Decide(state, command))
            .Code.ShouldBe(ErrorCodes.RecurringTaskCannotHaveDueDate);
    }

    [Fact]
    public void Completing_a_recurring_task_directly_is_rejected()
    {
        var state = Plain() with { Recurrence = RecurrenceRule.Daily(Today) };
        var command = new CompleteTask(User, Guid.NewGuid(), Now, Task);

        Should.Throw<DomainException>(() => TodoTaskDecider.Decide(state, command))
            .Code.ShouldBe(ErrorCodes.RecurringTaskCannotBeCompleted);
    }

    [Fact]
    public void Clearing_recurrence_on_a_plain_task_emits_nothing()
    {
        var command = new ClearRecurrence(User, Guid.NewGuid(), Now, Task);

        TodoTaskDecider.Decide(Plain(), command).ShouldBeEmpty();
    }

    [Fact]
    public void Apply_sets_and_clears_the_rule()
    {
        var commandId = Guid.NewGuid();
        var rule = RecurrenceRule.Daily(Today);

        var state = TodoTaskDecider.Apply(
            Plain(), new RecurrenceSet(User, Now, commandId, Task, rule))!;
        state.IsRecurring.ShouldBeTrue();

        state = TodoTaskDecider.Apply(
            state, new RecurrenceCleared(User, Now, commandId, Task))!;
        state.IsRecurring.ShouldBeFalse();
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test test/PSPad.Domain.Tests --filter TaskRecurrenceTests`
Expected: FAIL — `SetRecurrence` does not exist.

- [ ] **Step 3: Append the commands and events**

Append to `src/PSPad.Domain/Tasks/TaskCommands.cs`:

```csharp
public sealed record SetRecurrence(
    UserId UserId, Guid CommandId, DateTimeOffset IssuedAt,
    TaskId TaskId, RecurrenceRule Rule) : ICommand;

public sealed record ClearRecurrence(
    UserId UserId, Guid CommandId, DateTimeOffset IssuedAt, TaskId TaskId) : ICommand;
```

Append to `src/PSPad.Domain/Tasks/TaskEvents.cs`:

```csharp
public sealed record RecurrenceSet(
    UserId UserId, DateTimeOffset OccurredAt, Guid CommandId,
    TaskId TaskId, RecurrenceRule Rule) : IDomainEvent;

public sealed record RecurrenceCleared(
    UserId UserId, DateTimeOffset OccurredAt, Guid CommandId, TaskId TaskId) : IDomainEvent;
```

- [ ] **Step 4: Extend the decider**

In `src/PSPad.Domain/Tasks/TodoTaskDecider.cs`, add two arms to the `Decide`
switch, immediately before the `_ => DecideSteps(...)` default:

```csharp
            SetRecurrence set => SetRecurrenceOn(Require(state), set),
            ClearRecurrence clear => ClearRecurrenceOn(Require(state), clear),
```

Add the two handlers next to the other private methods:

```csharp
    static IReadOnlyList<IDomainEvent> SetRecurrenceOn(TodoTaskState state, SetRecurrence command)
    {
        command.Rule.Validate();

        var events = new List<IDomainEvent>();

        // A recurring task takes its dates from the rule, so an existing due
        // date is dropped explicitly rather than silently ignored (spec §3.3).
        if (state.DueDate is not null)
            events.Add(new TaskDueDateSet(
                command.UserId, command.IssuedAt, command.CommandId, state.Id, DueDate: null));

        if (state.Recurrence != command.Rule)
            events.Add(new RecurrenceSet(
                command.UserId, command.IssuedAt, command.CommandId, state.Id, command.Rule));

        return events;
    }

    static IReadOnlyList<IDomainEvent> ClearRecurrenceOn(TodoTaskState state, ClearRecurrence command) =>
        state.IsRecurring
            ? [new RecurrenceCleared(command.UserId, command.IssuedAt, command.CommandId, state.Id)]
            : [];
```

Add two arms to the `Apply` switch, before its `_ => ApplyStep(...)` default:

```csharp
            ({ } s, RecurrenceSet e) => s with { Recurrence = e.Rule, DueDate = null },
            ({ } s, RecurrenceCleared) => s with { Recurrence = null },
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test test/PSPad.Domain.Tests --filter TaskRecurrenceTests`
Expected: PASS, 7 tests.

- [ ] **Step 6: Commit**

```bash
git add src/PSPad.Domain/Tasks/ test/PSPad.Domain.Tests/Tasks/TaskRecurrenceTests.cs
git commit -m "feat(domain): set and clear recurrence, forbidding due dates on recurring tasks"
```

---

### Task 3: Occurrences

**Files:**
- Create: `src/PSPad.Domain/Tasks/Occurrence.cs`
- Modify: `src/PSPad.Domain/Tasks/TodoTaskState.cs`
- Modify: `src/PSPad.Domain/Tasks/TaskCommands.cs`
- Modify: `src/PSPad.Domain/Tasks/TaskEvents.cs`
- Modify: `src/PSPad.Domain/Tasks/TodoTaskDecider.cs`
- Test: `test/PSPad.Domain.Tests/Tasks/OccurrenceTests.cs`

**Interfaces:**
- Consumes: Tasks 1-2
- Produces: `enum OccurrenceStatus { Pending, Done, Skipped }`; `Occurrence(DateOnly Date, OccurrenceStatus Status, DateTimeOffset? CompletedAt)`; `TodoTaskState.Occurrences` as `IReadOnlyList<Occurrence>` (only completed days are stored); `TodoTaskState.StatusOn(DateOnly day, DateOnly today)`; commands `CompleteOccurrence(..., TaskId, DateOnly Date)`, `UncompleteOccurrence(..., TaskId, DateOnly Date)`; events `OccurrenceCompleted`, `OccurrenceUncompleted`

Only `Done` days are stored. `Pending` and `Skipped` are derived from the rule
plus today, which is what keeps the write model small and makes the
"never overdue" rule structural rather than a special case (spec §6).

- [ ] **Step 1: Write the failing test**

`test/PSPad.Domain.Tests/Tasks/OccurrenceTests.cs`:

```csharp
using PSPad.Domain.Common;
using PSPad.Domain.Tasks;
using Shouldly;

namespace PSPad.Domain.Tests.Tasks;

public class OccurrenceTests
{
    static readonly UserId User = UserId.New();
    static readonly ListId List = ListId.New();
    static readonly TaskId Task = TaskId.New();
    static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 9, 11);
    static readonly DateOnly Yesterday = new(2026, 9, 10);
    static readonly DateOnly Tomorrow = new(2026, 9, 12);

    static TodoTaskState Recurring(params Occurrence[] occurrences) => new(
        Task, User, List, "Read a book", null, Priority.None, false, null, [], null,
        RecurrenceRule.Daily(new DateOnly(2026, 9, 1)))
    { Occurrences = occurrences };

    [Fact]
    public void A_missed_past_day_is_skipped_not_pending()
    {
        Recurring().StatusOn(Yesterday, Today).ShouldBe(OccurrenceStatus.Skipped);
    }

    [Fact]
    public void A_completed_past_day_stays_done()
    {
        var state = Recurring(new Occurrence(Yesterday, OccurrenceStatus.Done, Now));

        state.StatusOn(Yesterday, Today).ShouldBe(OccurrenceStatus.Done);
    }

    [Fact]
    public void Today_is_pending_until_completed()
    {
        Recurring().StatusOn(Today, Today).ShouldBe(OccurrenceStatus.Pending);
    }

    [Fact]
    public void A_future_day_is_pending()
    {
        Recurring().StatusOn(Tomorrow, Today).ShouldBe(OccurrenceStatus.Pending);
    }

    [Fact]
    public void An_unscheduled_day_is_skipped()
    {
        var state = Recurring() with
        {
            Recurrence = RecurrenceRule.OnDays(new DateOnly(2026, 9, 1), DayOfWeek.Monday)
        };

        // 2026-09-11 is a Friday, so Monday-only recurrence does not schedule it.
        state.StatusOn(Today, Today).ShouldBe(OccurrenceStatus.Skipped);
    }

    [Fact]
    public void Completing_an_occurrence_emits_the_event_with_its_date()
    {
        var command = new CompleteOccurrence(User, Guid.NewGuid(), Now, Task, Today);

        var completed = TodoTaskDecider.Decide(Recurring(), command)
            .ShouldHaveSingleItem().ShouldBeOfType<OccurrenceCompleted>();

        completed.Date.ShouldBe(Today);
        completed.OccurredAt.ShouldBe(Now);
    }

    [Fact]
    public void Completing_an_unscheduled_day_is_rejected()
    {
        var state = Recurring() with
        {
            Recurrence = RecurrenceRule.OnDays(new DateOnly(2026, 9, 1), DayOfWeek.Monday)
        };
        var command = new CompleteOccurrence(User, Guid.NewGuid(), Now, Task, Today);

        Should.Throw<DomainException>(() => TodoTaskDecider.Decide(state, command))
            .Code.ShouldBe(ErrorCodes.DayNotScheduled);
    }

    [Fact]
    public void Completing_an_occurrence_of_a_non_recurring_task_is_rejected()
    {
        var state = Recurring() with { Recurrence = null };
        var command = new CompleteOccurrence(User, Guid.NewGuid(), Now, Task, Today);

        Should.Throw<DomainException>(() => TodoTaskDecider.Decide(state, command))
            .Code.ShouldBe(ErrorCodes.DayNotScheduled);
    }

    [Fact]
    public void Completing_the_same_day_twice_emits_nothing()
    {
        var state = Recurring(new Occurrence(Today, OccurrenceStatus.Done, Now));
        var command = new CompleteOccurrence(User, Guid.NewGuid(), Now, Task, Today);

        TodoTaskDecider.Decide(state, command).ShouldBeEmpty();
    }

    [Fact]
    public void Apply_records_and_removes_completed_days()
    {
        var commandId = Guid.NewGuid();

        var state = TodoTaskDecider.Apply(
            Recurring(), new OccurrenceCompleted(User, Now, commandId, Task, Today))!;
        state.StatusOn(Today, Today).ShouldBe(OccurrenceStatus.Done);

        state = TodoTaskDecider.Apply(
            state, new OccurrenceUncompleted(User, Now, commandId, Task, Today))!;
        state.StatusOn(Today, Today).ShouldBe(OccurrenceStatus.Pending);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test test/PSPad.Domain.Tests --filter OccurrenceTests`
Expected: FAIL — `Occurrence` does not exist.

- [ ] **Step 3: Write the occurrence type**

`src/PSPad.Domain/Tasks/Occurrence.cs`:

```csharp
namespace PSPad.Domain.Tasks;

public enum OccurrenceStatus
{
    Pending = 0,
    Done = 1,
    Skipped = 2
}

/// <summary>
/// One scheduled day of a recurring task. Only completed days are persisted —
/// pending and skipped are derived from the rule and the current date (spec §6).
/// </summary>
public sealed record Occurrence(
    DateOnly Date,
    OccurrenceStatus Status,
    DateTimeOffset? CompletedAt);
```

- [ ] **Step 4: Extend the task state**

In `src/PSPad.Domain/Tasks/TodoTaskState.cs`, add an init-only collection and the
status function to `TodoTaskState`:

```csharp
    /// <summary>Completed days only. Everything else is derived by StatusOn.</summary>
    public IReadOnlyList<Occurrence> Occurrences { get; init; } = [];

    /// <summary>
    /// The status of one day. A scheduled day in the past with no completion is
    /// Skipped, never Pending — this is what stops recurring tasks from ever
    /// becoming overdue (spec §5, §6).
    /// </summary>
    public OccurrenceStatus StatusOn(DateOnly day, DateOnly today)
    {
        if (Occurrences.FirstOrDefault(o => o.Date == day) is { Status: OccurrenceStatus.Done })
            return OccurrenceStatus.Done;

        if (Recurrence is null || !Recurrence.Occurs(day))
            return OccurrenceStatus.Skipped;

        return day < today ? OccurrenceStatus.Skipped : OccurrenceStatus.Pending;
    }
```

- [ ] **Step 5: Append the commands, events and decider arms**

Append to `src/PSPad.Domain/Tasks/TaskCommands.cs`:

```csharp
public sealed record CompleteOccurrence(
    UserId UserId, Guid CommandId, DateTimeOffset IssuedAt,
    TaskId TaskId, DateOnly Date) : ICommand;

public sealed record UncompleteOccurrence(
    UserId UserId, Guid CommandId, DateTimeOffset IssuedAt,
    TaskId TaskId, DateOnly Date) : ICommand;
```

Append to `src/PSPad.Domain/Tasks/TaskEvents.cs`:

```csharp
public sealed record OccurrenceCompleted(
    UserId UserId, DateTimeOffset OccurredAt, Guid CommandId,
    TaskId TaskId, DateOnly Date) : IDomainEvent;

public sealed record OccurrenceUncompleted(
    UserId UserId, DateTimeOffset OccurredAt, Guid CommandId,
    TaskId TaskId, DateOnly Date) : IDomainEvent;
```

In `src/PSPad.Domain/Tasks/TodoTaskDecider.cs`, add to the `Decide` switch before
its default arm:

```csharp
            CompleteOccurrence complete => CompleteOccurrenceOn(Require(state), complete),
            UncompleteOccurrence uncomplete => UncompleteOccurrenceOn(Require(state), uncomplete),
```

Add the handlers:

```csharp
    static IReadOnlyList<IDomainEvent> CompleteOccurrenceOn(
        TodoTaskState state, CompleteOccurrence command)
    {
        if (state.Recurrence is null || !state.Recurrence.Occurs(command.Date))
            throw new DomainException(
                ErrorCodes.DayNotScheduled, "That day is not scheduled for this task.");

        var alreadyDone = state.Occurrences
            .Any(o => o.Date == command.Date && o.Status == OccurrenceStatus.Done);

        return alreadyDone
            ? []
            : [new OccurrenceCompleted(
                command.UserId, command.IssuedAt, command.CommandId, state.Id, command.Date)];
    }

    static IReadOnlyList<IDomainEvent> UncompleteOccurrenceOn(
        TodoTaskState state, UncompleteOccurrence command)
    {
        var done = state.Occurrences
            .Any(o => o.Date == command.Date && o.Status == OccurrenceStatus.Done);

        return done
            ? [new OccurrenceUncompleted(
                command.UserId, command.IssuedAt, command.CommandId, state.Id, command.Date)]
            : [];
    }
```

Add to the `Apply` switch before its default arm:

```csharp
            ({ } s, OccurrenceCompleted e) => s with
            {
                Occurrences =
                [
                    .. s.Occurrences.Where(o => o.Date != e.Date),
                    new Occurrence(e.Date, OccurrenceStatus.Done, e.OccurredAt)
                ]
            },
            ({ } s, OccurrenceUncompleted e) => s with
            {
                Occurrences = [.. s.Occurrences.Where(o => o.Date != e.Date)]
            },
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test test/PSPad.Domain.Tests --filter OccurrenceTests`
Expected: PASS, 10 tests.

- [ ] **Step 7: Commit**

```bash
git add src/PSPad.Domain/Tasks/ test/PSPad.Domain.Tests/Tasks/OccurrenceTests.cs
git commit -m "feat(domain): add occurrences with derived pending and skipped days"
```

---

### Task 4: The Today rule

**Files:**
- Create: `src/PSPad.Domain/Today/TodayRule.cs`
- Create: `src/PSPad.Domain/Today/TodayCandidate.cs`
- Test: `test/PSPad.Domain.Tests/Today/TodayRuleTests.cs`

**Interfaces:**
- Consumes: Tasks 1-3, plan 02 Tasks 5-6
- Produces: `TodayCandidate(TaskId TaskId, string Name, DateOnly? DueDate, DateOnly? NextStepDate, Priority Priority, bool Starred, bool Completed, RecurrenceRule? Recurrence, IReadOnlyList<DateOnly> CompletedDays)`; `TodayRule.Evaluate(TodayCandidate candidate, DateOnly today)` returning `TodayVerdict?` where `TodayVerdict(bool Overdue, DateOnly ReasonDate)`; `TodayRule.From(TodoTaskState state)` building a candidate from full task state

`TodayCandidate` is a flat snapshot rather than the full aggregate, because the
server evaluates this rule over a projection row (plan 04 Task 6), not over
rehydrated aggregates. The client evaluates the same rule over the same shape.

- [ ] **Step 1: Write the failing test**

`test/PSPad.Domain.Tests/Today/TodayRuleTests.cs`:

```csharp
using PSPad.Domain.Common;
using PSPad.Domain.Tasks;
using PSPad.Domain.Today;
using Shouldly;

namespace PSPad.Domain.Tests.Today;

public class TodayRuleTests
{
    static readonly DateOnly Today = new(2026, 9, 11);
    static readonly DateOnly Yesterday = new(2026, 9, 10);
    static readonly DateOnly Tomorrow = new(2026, 9, 12);

    static TodayCandidate Candidate(
        DateOnly? dueDate = null,
        DateOnly? nextStepDate = null,
        bool completed = false,
        RecurrenceRule? recurrence = null,
        IReadOnlyList<DateOnly>? completedDays = null) =>
        new(TaskId.New(), "Read a book", dueDate, nextStepDate,
            Priority.None, Starred: false, completed, recurrence, completedDays ?? []);

    [Fact]
    public void A_task_due_today_is_on_today_and_not_overdue()
    {
        var verdict = TodayRule.Evaluate(Candidate(dueDate: Today), Today);

        verdict.ShouldNotBeNull();
        verdict.Overdue.ShouldBeFalse();
        verdict.ReasonDate.ShouldBe(Today);
    }

    [Fact]
    public void A_task_due_yesterday_is_overdue()
    {
        var verdict = TodayRule.Evaluate(Candidate(dueDate: Yesterday), Today);

        verdict.ShouldNotBeNull();
        verdict.Overdue.ShouldBeTrue();
        verdict.ReasonDate.ShouldBe(Yesterday);
    }

    [Fact]
    public void A_task_due_tomorrow_is_not_on_today()
    {
        TodayRule.Evaluate(Candidate(dueDate: Tomorrow), Today).ShouldBeNull();
    }

    [Fact]
    public void A_task_whose_next_step_is_due_today_is_on_today()
    {
        var verdict = TodayRule.Evaluate(Candidate(nextStepDate: Today), Today);

        verdict.ShouldNotBeNull();
        verdict.ReasonDate.ShouldBe(Today);
    }

    [Fact]
    public void The_earlier_of_the_task_date_and_the_step_date_is_the_reason()
    {
        var verdict = TodayRule.Evaluate(
            Candidate(dueDate: Today, nextStepDate: Yesterday), Today);

        verdict.ShouldNotBeNull();
        verdict.Overdue.ShouldBeTrue();
        verdict.ReasonDate.ShouldBe(Yesterday);
    }

    [Fact]
    public void A_completed_task_is_never_on_today()
    {
        TodayRule.Evaluate(Candidate(dueDate: Yesterday, completed: true), Today).ShouldBeNull();
    }

    [Fact]
    public void A_task_with_no_dates_is_not_on_today()
    {
        TodayRule.Evaluate(Candidate(), Today).ShouldBeNull();
    }

    [Fact]
    public void A_recurring_task_scheduled_today_is_on_today_and_never_overdue()
    {
        var verdict = TodayRule.Evaluate(
            Candidate(recurrence: RecurrenceRule.Daily(new DateOnly(2026, 9, 1))), Today);

        verdict.ShouldNotBeNull();
        verdict.Overdue.ShouldBeFalse();
        verdict.ReasonDate.ShouldBe(Today);
    }

    [Fact]
    public void A_recurring_task_missed_yesterday_is_not_overdue_today()
    {
        // The whole point of spec §5: "Read a book" untouched yesterday must
        // show as today's pending occurrence, never as an overdue debt.
        var verdict = TodayRule.Evaluate(
            Candidate(recurrence: RecurrenceRule.Daily(new DateOnly(2026, 9, 1))), Today);

        verdict!.Overdue.ShouldBeFalse();
    }

    [Fact]
    public void A_recurring_task_already_done_today_leaves_today()
    {
        var verdict = TodayRule.Evaluate(
            Candidate(
                recurrence: RecurrenceRule.Daily(new DateOnly(2026, 9, 1)),
                completedDays: [Today]),
            Today);

        verdict.ShouldBeNull();
    }

    [Fact]
    public void A_recurring_task_not_scheduled_today_is_not_on_today()
    {
        var verdict = TodayRule.Evaluate(
            Candidate(recurrence: RecurrenceRule.OnDays(
                new DateOnly(2026, 9, 1), DayOfWeek.Monday)),
            Today);

        verdict.ShouldBeNull();
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test test/PSPad.Domain.Tests --filter TodayRuleTests`
Expected: FAIL — the `Today` namespace does not exist.

- [ ] **Step 3: Write the candidate snapshot**

`src/PSPad.Domain/Today/TodayCandidate.cs`:

```csharp
using PSPad.Domain.Common;
using PSPad.Domain.Tasks;

namespace PSPad.Domain.Today;

/// <summary>
/// Flat snapshot of everything the Today rule needs. The server builds these
/// from a projection row, the client from its local replica — the rule itself
/// never sees an aggregate.
/// </summary>
public sealed record TodayCandidate(
    TaskId TaskId,
    string Name,
    DateOnly? DueDate,
    DateOnly? NextStepDate,
    Priority Priority,
    bool Starred,
    bool Completed,
    RecurrenceRule? Recurrence,
    IReadOnlyList<DateOnly> CompletedDays);

public sealed record TodayVerdict(bool Overdue, DateOnly ReasonDate);
```

- [ ] **Step 4: Write the rule**

`src/PSPad.Domain/Today/TodayRule.cs`:

```csharp
using PSPad.Domain.Tasks;

namespace PSPad.Domain.Today;

public static class TodayRule
{
    /// <summary>
    /// Spec §5. Returns null when the task does not belong on Today.
    /// Recurring tasks are judged only by today's occurrence and are never
    /// overdue; a missed day stays behind as skipped.
    /// </summary>
    public static TodayVerdict? Evaluate(TodayCandidate candidate, DateOnly today)
    {
        if (candidate.Completed)
            return null;

        if (candidate.Recurrence is { } recurrence)
        {
            if (!recurrence.Occurs(today))
                return null;

            if (candidate.CompletedDays.Contains(today))
                return null;

            return new TodayVerdict(Overdue: false, ReasonDate: today);
        }

        var reason = Earliest(candidate.DueDate, candidate.NextStepDate);

        if (reason is not { } date || date > today)
            return null;

        return new TodayVerdict(Overdue: date < today, ReasonDate: date);
    }

    static DateOnly? Earliest(DateOnly? first, DateOnly? second) =>
        (first, second) switch
        {
            ({ } a, { } b) => a <= b ? a : b,
            ({ } a, null) => a,
            (null, { } b) => b,
            _ => null
        };

    /// <summary>Builds a candidate from full aggregate state, for client-side evaluation.</summary>
    public static TodayCandidate From(TodoTaskState state) => new(
        state.Id,
        state.Name,
        state.DueDate,
        state.NextStep?.DueDate,
        state.Priority,
        state.Starred,
        state.IsCompleted,
        state.Recurrence,
        [.. state.Occurrences
            .Where(o => o.Status == OccurrenceStatus.Done)
            .Select(o => o.Date)]);
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test test/PSPad.Domain.Tests --filter TodayRuleTests`
Expected: PASS, 11 tests.

- [ ] **Step 6: Run the whole domain suite**

Run: `dotnet test test/PSPad.Domain.Tests`
Expected: PASS, everything from plans 02 and 03.

- [ ] **Step 7: Commit**

```bash
git add src/PSPad.Domain/Today/ test/PSPad.Domain.Tests/Today/
git commit -m "feat(domain): add the today rule with recurrence never overdue"
```

---

### Task 5: Completion history for a date range

**Files:**
- Create: `src/PSPad.Domain/Tasks/OccurrenceHistory.cs`
- Test: `test/PSPad.Domain.Tests/Tasks/OccurrenceHistoryTests.cs`

**Interfaces:**
- Consumes: Tasks 1-3
- Produces: `OccurrenceHistory.Between(TodoTaskState state, DateOnly from, DateOnly to, DateOnly today)` returning `IReadOnlyList<Occurrence>` — every scheduled day in the range with its derived status

This is what the habits subsystem and the charts screen will read. Building it
now costs one small function and means neither of those later subsystems has to
reach into the event stream.

- [ ] **Step 1: Write the failing test**

`test/PSPad.Domain.Tests/Tasks/OccurrenceHistoryTests.cs`:

```csharp
using PSPad.Domain.Common;
using PSPad.Domain.Tasks;
using Shouldly;

namespace PSPad.Domain.Tests.Tasks;

public class OccurrenceHistoryTests
{
    static readonly UserId User = UserId.New();
    static readonly ListId List = ListId.New();
    static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 9, 11);

    static TodoTaskState Recurring(RecurrenceRule rule, params DateOnly[] doneDays) => new(
        TaskId.New(), User, List, "Read a book", null, Priority.None, false, null, [], null, rule)
    {
        Occurrences = [.. doneDays.Select(d => new Occurrence(d, OccurrenceStatus.Done, Now))]
    };

    [Fact]
    public void Returns_one_entry_per_scheduled_day_in_the_range()
    {
        var state = Recurring(RecurrenceRule.Daily(new DateOnly(2026, 9, 9)));

        var history = OccurrenceHistory.Between(
            state, new DateOnly(2026, 9, 9), Today, Today);

        history.Select(o => o.Date)
            .ShouldBe([new DateOnly(2026, 9, 9), new DateOnly(2026, 9, 10), Today]);
    }

    [Fact]
    public void Marks_past_days_done_or_skipped_and_today_pending()
    {
        var state = Recurring(
            RecurrenceRule.Daily(new DateOnly(2026, 9, 9)),
            new DateOnly(2026, 9, 9));

        var history = OccurrenceHistory.Between(
            state, new DateOnly(2026, 9, 9), Today, Today);

        history.Select(o => o.Status).ShouldBe(
            [OccurrenceStatus.Done, OccurrenceStatus.Skipped, OccurrenceStatus.Pending]);
    }

    [Fact]
    public void Skips_days_the_rule_does_not_schedule()
    {
        var state = Recurring(
            RecurrenceRule.OnDays(new DateOnly(2026, 9, 1), DayOfWeek.Friday));

        var history = OccurrenceHistory.Between(
            state, new DateOnly(2026, 9, 9), Today, Today);

        history.ShouldHaveSingleItem().Date.ShouldBe(Today);
    }

    [Fact]
    public void A_non_recurring_task_has_no_history()
    {
        var state = Recurring(RecurrenceRule.Daily(Today)) with { Recurrence = null };

        OccurrenceHistory.Between(state, new DateOnly(2026, 9, 9), Today, Today).ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test test/PSPad.Domain.Tests --filter OccurrenceHistoryTests`
Expected: FAIL — `OccurrenceHistory` does not exist.

- [ ] **Step 3: Write the function**

`src/PSPad.Domain/Tasks/OccurrenceHistory.cs`:

```csharp
namespace PSPad.Domain.Tasks;

public static class OccurrenceHistory
{
    /// <summary>
    /// Every scheduled day between `from` and `to` inclusive, with its derived
    /// status. Nothing is stored for pending or skipped days — they come from
    /// the rule plus `today` (spec §6). Habits and the charts screen read this.
    /// </summary>
    public static IReadOnlyList<Occurrence> Between(
        TodoTaskState state, DateOnly from, DateOnly to, DateOnly today)
    {
        if (state.Recurrence is null)
            return [];

        var days = new List<Occurrence>();

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (!state.Recurrence.Occurs(day))
                continue;

            var status = state.StatusOn(day, today);
            var completedAt = state.Occurrences.FirstOrDefault(o => o.Date == day)?.CompletedAt;

            days.Add(new Occurrence(day, status, completedAt));
        }

        return days;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test test/PSPad.Domain.Tests --filter OccurrenceHistoryTests`
Expected: PASS, 4 tests.

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.Domain/Tasks/OccurrenceHistory.cs test/PSPad.Domain.Tests/Tasks/OccurrenceHistoryTests.cs
git commit -m "feat(domain): derive occurrence history over a date range"
```

---

## Done when

- `dotnet test test/PSPad.Domain.Tests` is green.
- A recurring task missed yesterday shows today as pending, never overdue — covered by an explicit test.
- The Today rule exists once, in the domain, and takes `today` as a parameter.
- Occurrence history over a range is available for the habits and charts subsystems.
