# List filter, sort and month calendar — design

Status: Designed, not built. Issue #198. No ADR: the change is client-only,
adds no command, event, field or endpoint, and keeps its state per device.
Mockup (layout and content only, never colours or CSS):
<https://claude.ai/artifact/Jdi338XjQtVGxtTpBUwwB4>.

Build order: **after** #202 (step deadlines, `specs/step-deadlines-design.md`)
and #200 (goal on the task card). Both are inputs here. #202 makes a step's
deadline part of a task's date, and this design reuses that date. #200 adds
the goal piece to `TaskRow`, and the goal filter matches goals the same way.

Two asks from one issue:

1. **Filter and sort** a task list, so the right task is easy to find.
2. **See how the list's tasks fall across the month**, in a calendar view of
   the list.

## 1. Scope

In:

- A toolbar on a `Tasks` list screen with two separate controls, **Filter**
  and **Sort**, and a **List | Calendar** switch.
- Filter groups: **When** (Overdue, Today, This week, Later, No date),
  **Priority** (High, Medium, Low, None) and **Other** (Starred, Has a goal,
  one chip per goal).
- Sort orders: **Date** (the default), **Priority**, **Name**, **Recently
  added**, each with a direction.
- A month calendar of the list's open tasks, with a day detail.
- Remembering the filter, sort, direction and view per list, on this device.
- Pure, WASM-safe functions in `PSPad.Module.Tasks` for the date, the filter,
  the sort and the month.

Out:

- Text search inside a list. The sidebar search already finds tasks by name.
- Manual drag order of tasks. It needs a stored position per task, which is
  a domain change with its own ADR.
- Filter, sort or calendar on My Day, the area board, the goal screen or
  reference lists.
- Syncing the choices between devices (see §7).
- A week view, or a calendar across all lists.

## 2. One date per task

Sort, the **When** filter and the calendar all read the same date: the date
that puts a task on My Day. The rule stays in one place, `TodayRule`
(AGENTS.md §2).

- `TodayRule.TriggerOf(TodoTask task)` becomes public. Today it is the
  private `EarliestTrigger`. It returns the earlier of the task's `DueOn` and
  its earliest unchecked step deadline (`TodoTask.EarliestDueStep`, #202), or
  `null`. It is meaningful for one-time tasks only.
- `TodayRule.DateOf(TodoTask task, DateOnly today)` is new and public:
  - A one-time task returns `TriggerOf(task)`. Past dates stay past, so the
    task can be Overdue.
  - A recurring task returns its next unticked occurrence on or after
    `today`: the first day `d` in `today … min(Until, today + 400 days)` with
    `task.OccursOn(d)` and `d` not in `CompletedDays`. Otherwise it returns
    `null`. A recurring task is never before `today`, so it is never Overdue.
    The 400-day horizon covers a yearly rule. A rule with no occurrence in
    range (every 2+ years, far off) counts as undated.
  - `Plan`, `Select` and `ComingUp` keep calling the same private pieces, so
    their output does not change. `DateOf` does not apply lead time, because
    a list shows every task whatever its lead time.

## 3. Filter

`ListFilter` (record, `PSPad.Module.Tasks/Lists/View/ListFilter.cs`,
namespace `PSPad.Module.Tasks.Lists`):

```csharp
public sealed record ListFilter(
    IReadOnlySet<WhenBucket> When,
    IReadOnlySet<Priority> Priorities,
    bool Starred,
    bool HasGoal,
    IReadOnlySet<Guid> GoalIds)
{
    public static readonly ListFilter None = …;
    public bool IsEmpty { get; }
    public int ActiveCount { get; }
    public bool Matches(TodoTask task, DateOnly today, DayOfWeek firstDayOfWeek, IReadOnlyList<Goal> goals);
}

public enum WhenBucket { Overdue, Today, ThisWeek, Later, NoDate }
```

- **Buckets.** A task falls in exactly one bucket by `DateOf(task, today)`:
  - **Overdue:** before `today`.
  - **Today:** `today`.
  - **This week:** after `today`, up to the last day of the week holding
    `today`. The week starts on `firstDayOfWeek`, passed in from the client,
    the same as `WeekStrip` uses (`CultureInfo.CurrentCulture`). The domain
    stays culture-free.
  - **Later:** after this week.
  - **No date:** `null`.

  On a week's last day, This week matches nothing.
- **Combining.** Groups combine with AND and choices inside a group with OR.
  An empty group matches everything. **Other** is one group: Starred, Has a
  goal and each goal chip are OR'ed together.
- **Goals.** Goals are matched at render time from `Goal.TaskIds`
  (`Goal.Holding`), exactly as in #200. The task stores nothing about its
  goal. The goal chips list every goal that isn't deleted and holds at least
  one task of this list. A stored goal id that is no longer offered is
  ignored, not shown.
- **Completed tasks** go through the same filter. Their bucket comes from
  the same date, but **Overdue never matches a completed task**: a done task
  is not late. So with only Overdue chosen, the Completed section is empty.
- `ActiveCount` counts the chosen chips. Starred and Has a goal count one
  each.

## 4. Sort

`ListSort` (`PSPad.Module.Tasks/Lists/View/ListSort.cs`):

```csharp
public enum ListOrder { Date, Priority, Name, Added }

public sealed record ListSort(ListOrder Order, bool Descending)
{
    public static readonly ListSort Default = new(ListOrder.Date, false);
    public IReadOnlyList<TodoTask> Apply(IEnumerable<TodoTask> tasks, DateOnly today, StringComparer names);
}
```

| Order | Ascending (the default direction) | Ties, in order |
|-------|-----------------------------------|----------------|
| Date | `DateOf` earliest first, **undated last in both directions** | priority high first, name, id |
| Priority | High, Medium, Low, None | `DateOf` (undated last), name, id |
| Name | A → Z with `names` (the client passes `StringComparer.Create(CurrentCulture, ignoreCase: true)`, so Polish letters sort right) | id |
| Added | `CreatedAt` newest first, missing `CreatedAt` last | name, id |

`Descending` reverses the primary key only. Ties keep their order, and
undated or missing values stay last. The id tie-break keeps the order
stable across renders, so rows don't jump on a sync.

The direction labels follow the order:

| Order | Ascending | Descending |
|-------|-----------|------------|
| Date | Earliest first | Latest first |
| Priority | High first | Low first |
| Name | A → Z | Z → A |
| Added | Newest first | Oldest first |

## 5. Month

`ListCalendar` (static, `PSPad.Module.Tasks/Lists/View/ListCalendar.cs`):

```csharp
public static IReadOnlyList<CalendarDay> Month(
    IEnumerable<TodoTask> tasks, int year, int month, DateOnly today);

public sealed record CalendarDay(DateOnly Day, IReadOnlyList<CalendarEntry> Entries);

public sealed record CalendarEntry(Guid TaskId, bool Overdue, bool Recurring);
```

- It takes `year` and `month` as integers, because `YearMonth` lives in
  `PSPad.Module.Money` and Tasks may not reference it (AGENTS.md §2). The
  App converts the month.
- It returns one `CalendarDay` for each day of the month that has entries.
  Callers filter first and sort afterwards, so `Month` neither filters nor
  orders.
- **One-time, open:** one entry on `TriggerOf(task)` when that day is in the
  month, with `Overdue` true when the day is before `today`.
- **Recurring:** one entry on each day `d` of the month with
  `d >= today`, `task.OccursOn(d)` and `d` unticked. Past days stay empty,
  because a recurring task is never overdue (AGENTS.md §2). Lead time is
  ignored.
- **Completed or deleted** one-time tasks, and ended repeats, are left out.
- **Undated** tasks are not placed. The screen counts them separately (§6.4).

## 6. Screen

All of this is on `Pages/ListPage.razor` for `ListKind.Tasks` only, built
from MudBlazor components and existing `pspad-*` classes in the app's
current look (`specs/ui-spec.md`). Every new string goes in `ListsStrings`
in English and Polish.

### 6.1 Toolbar

`Components/ListToolbar.razor` sits under the page heading and above the
rows, in one `d-flex align-center gap-2 flex-wrap` line:

- **Filter.** A `MudMenu` whose activator is a small outlined button with
  `FilterList` and "Filter". With choices active it shows a primary badge
  holding `ActiveCount`.
- **Sort.** A second `MudMenu` whose activator shows `Sort`, the order's
  name and an up or down arrow for the direction.
- A spacer, then the **List | Calendar** `MudToggleGroup` (`Size.Small`,
  `Color.Primary`, `SelectionMode.SingleSelection`), the same pattern as
  `BudgetPage`'s views. Below `md` the items show icons only (`ViewAgenda`,
  `CalendarMonth`), each with an `aria-label`.

The two menus are separate controls on purpose. Filter changes which tasks
show, and Sort changes their order.

### 6.2 Filter menu and active chips

- **Menu.** Three captioned groups (When, Priority, Other). Each group is a
  `MudChipSet` of filter chips with `SelectionMode.MultiSelection`. The
  Overdue chip uses `Color.Error` text. The footer holds **Clear**, a text
  button that resets to `ListFilter.None`, and "N of M tasks", which counts
  open tasks. A change applies at once, with no Apply button.
- **Active chips.** Under the toolbar, each non-empty group shows as one
  closable `MudChip`, for example "Overdue, Today, This week" or
  "High, Medium". Closing a chip clears that group. A **Clear** link follows
  the chips.
- **No match.** When open tasks exist but none match, the rows area shows
  "No tasks match" with a **Clear** button inside a `RowGroup`. It is not
  `EmptyState`, which is only for a list with no tasks at all.
- The heading's subtitle ("Home · 8 open") keeps counting every open task.
  The filter changes the view, not the list.

### 6.3 Sort menu

The menu lists four `MudMenuItem`s: Date (with a muted "default" hint),
Priority, Name and Recently added. The chosen one has a check. Under a
divider, a two-item `MudToggleGroup` sets the direction, using the labels
from §4. The `RowGroup` of open tasks, the Completed section and the
calendar's day lists all use the same `ListSort`.

### 6.4 Calendar view

- **Month switcher.** `MonthStrip` (today a Budgets component) moves the
  month. Its Budgets-only strings ("This month", "Pick another month",
  "Month") move to `SharedStrings` so a list can use it too. It opens on the
  current month.
- **Grid.** `Components/ListMonthView.razor` is a seven-column CSS grid
  inside an outlined `MudPaper`, and it starts on the culture's first day of
  week, like `WeekStrip`. Days from the neighbouring months fill the first
  and last rows, muted, and show their own entries. Today's number is
  outlined in `Color.Primary`, and the selected day is filled.
- **Cell from `md` up:** the day number, then up to **two** entries in sort
  order, each a one-line caption with the task's priority bar (the
  `pspad-priority-*` classes) and its name cut with an ellipsis. An overdue
  one-time entry uses `Color.Error`, and a recurring entry carries the
  `Repeat` icon. A third or later entry adds "+N more".
- **Cell below `md`:** the day number with a dot and a count. The dot is
  `Color.Error` when the day holds an overdue entry.
- **Day detail.** Clicking a day selects it, and its tasks render as normal
  `TaskRow`s (tick, star, open) in a `RowGroup` under a heading such as
  "Tue, 27 Oct · 3". From `lg` up the detail sits beside the grid, and below
  that it sits under it. The default selection is today when today is in
  the month shown, otherwise nothing. The selection is not remembered.
- **Undated.** Under the grid, "N tasks have no date" with a **Show them**
  link switches to List with When = No date. The line is hidden at zero.
- **Filter and sort.** The active filter applies to the calendar, and the
  sort orders entries inside a day and in the day detail.
- **Accessibility.** Each day is a `button` with an `aria-label` such as
  "27 October, 3 tasks". The grid is a `role="grid"` with column headers.

### 6.5 List view

The rows stay as they are today (`RowGroup Columns`, `TaskRow`, `AddRow`
last), now filtered and in `ListSort` order. The Completed expansion panel
keeps its place, also filtered and sorted.

## 7. Remembered per device

`State/ListScreenState.cs`, a scoped service built the same way as
`CardCollapseState`, keeps one JSON value per list in `localStorage` under
`pspad.listview.{listId}`:

```json
{ "when": ["Overdue","Today"], "priority": ["High"], "starred": false,
  "hasGoal": false, "goals": [], "order": "Date", "descending": false,
  "view": "List" }
```

- **Reads.** A failed read (`JSException`, malformed JSON, unknown enum
  names) falls back to the defaults: no filter, Date ascending, List.
  Unknown names are dropped one by one.
- **Writes.** Every change writes the whole value. A value equal to the
  defaults removes the key.
- **Per device.** Nothing syncs and no command is sent. A different device
  starts with the defaults. List ids are GUIDs, so a value left by a
  previous user on a shared device matches none of the next user's lists.
- **Not synced, on purpose.** Storing the choices on the per-user `ListView`
  (Presentation, `adr/0051`) would sync them across devices. The maintainer
  chose per device for #198, so the screen stays a view preference like a
  collapsed card, and Presentation gains no commands. A later wish to sync
  them would need a `ListView` extension and its own ADR.

## 8. Testing

Every test class carries `[UnitTest]` or `[IntegrationTest]`
(`specs/testing-spec.md`).

- **Module unit tests** (`test/PSPad.Module.Tasks.Tests/Lists/View/`):
  - `TodayRule.DateOf`: one-time with due only, step only, both (the earlier
    wins), checked step ignored, undated; recurring next unticked occurrence,
    today ticked → next day, ended → `null`, beyond the horizon → `null`.
  - `ListFilter`: each bucket; the week edge with Monday and Sunday starts;
    AND across groups and OR within; goal match from `Goal.TaskIds` and a
    deleted goal ignored; a completed task never Overdue.
  - `ListSort`: each order and direction; undated last both ways; ties;
    a culture comparer with Polish names.
  - `ListCalendar.Month`: one-time on its trigger, including a step
    deadline; overdue flag; recurring only from today; month edges; a
    completed task left out; a leap-year February.
- **The existing Today rule suites stay green unchanged.** `TriggerOf`
  becoming public must not change `Plan` or `Select`.
- **bUnit** (`test/PSPad.App.Tests/`):
  - The toolbar: badge count, chips, Clear, the sort check and direction
    labels.
  - The list page: filtered and sorted rows, "No tasks match", the subtitle
    still counting every open task.
  - `ListScreenState`: round trip, a bad value falls back to the defaults,
    defaults remove the key.
  - The calendar: names and "+N more" from `md` up, dots below `md`, day
    detail rows, the undated link.
  - English and Polish strings.
- **E2E** (`test/e2e/scenarios/22-list-filter-calendar.spec.ts`): seed a
  list, filter to High, sort by Name, reload and check the filter and sort
  are remembered. Switch to Calendar, open a day with two tasks and tick
  one from the day detail.
