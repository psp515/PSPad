# My Day — one day at a time, with times — design

Status: Designed. Issue #112. Mockups:
[PSPad My Day canvas](https://claude.ai/artifact/Q4CSixpaBmT26cS4M6dFfa)
(private to the maintainer).

My Day today is a list of lists — overdue, today, starred, tomorrow, goals,
upcoming. The issue asks for a page about **one day**: pick the day, see its
tasks hour by hour where a time is set, then the work for "any time" — the
GTD free-time list — and what got finished that day. Goals leave the page.

This file is temporary. The last step of the implementation folds it into
`specs/ui-spec.md` and `specs/backend-spec.md` and deletes it (§8).

## 1. Scope

In:

- An optional **expected start and end time** on a task — `TodoTask.Time`,
  command `SetTaskTime` — set in the task panel, also while adding.
- `TodayRule.Plan` for **any day**, not only today, with the sections
  Overdue, Schedule, Any time, Starred, Coming up, Completed.
- My Day: a **day picker** (‹ date ›) in the header, the day in the URL,
  sections per §4, Goals in progress removed.
- `TaskRow` shows the time.
- **Sidebar restyle** at `md`+ to the canvas look (§5).

Out:

- An hourly calendar grid, durations as block heights, overlap layout.
  Tasks may share a time; nothing checks it.
- Times on steps.
- Tasks running past midnight (end ≤ start is rejected).
- Notifications at the start time — the Reminders subsystem (#8) will read
  the stored time later.
- Any change to the `GET /api/today` contract beyond the additive
  `TodayEntry.Time`.

No ADR: a field and a command inside the existing decisions (AD-2, AD-3,
AD-7, AD-8 unchanged).

## 2. Domain

### 2.1 Task time

```csharp
public sealed record TaskTime(TimeOnly Start, TimeOnly? End)
{
    public static TaskTime Of(TimeOnly start, TimeOnly? end);
}
```

- In `Tasks/` beside `LeadTime`. `Of` (and `Decide`, for a hand-built
  record) rejects `End <= Start` with
  `DomainRejectedException("A task must end after it starts.")`.
- A wall-clock time read in the user's stored time zone, like `DueOn` — no
  offset, no instant.
- `TodoTask.Time` (`TaskTime?`, `[JsonInclude]`). Documents stored before
  the field read `null`.
- Command `SetTaskTime(CommandId, UserId, TaskId, TaskTime? Time)`, event
  `TaskTimeSet(AggregateId, UserId, At, TaskTime? Time)`, handler
  `SetTaskTimeHandler`, all in `Tasks/SetTime/`, copying
  `Tasks/SetLeadTime/`: same `ListAccess` grant (owner or member), no event
  when unchanged, `null` clears.
- A recurring task's time is every occurrence's time.
- Removing a one-time task's due date keeps the time; an undated task is on
  no day, so the time is ignored until a date returns.

### 2.2 Day plan

```csharp
public sealed record DayPlan(
    IReadOnlyList<TodayEntry> Overdue,
    IReadOnlyList<TodayEntry> Scheduled,
    IReadOnlyList<TodayEntry> AnyTime,
    IReadOnlyList<TodayEntry> Starred,
    IReadOnlyList<TodayEntry> ComingUp,
    IReadOnlyList<TodayEntry> Completed);

public static DayPlan Plan(IEnumerable<TodoTask> tasks, DateOnly day, DateOnly today, TimeZoneInfo zone);
```

`day` is the day on screen, `today` the user's today (`State.Today`). A
task's date is still its earliest trigger — due date or next unchecked step.
`TodayEntry` gains `TaskTime? Time` (last positional parameter).

By where `day` sits:

| Section | `day < today` | `day == today` | `day > today` |
|---|---|---|---|
| Overdue | — | `Select`'s overdue entries | — |
| Scheduled | — | due on `day`, with a time | due on `day`, with a time |
| AnyTime | — | due on `day`, no time | due on `day`, no time |
| Starred | — | starred, open, one-time, trigger none or `> day` | same |
| ComingUp | — | ahead of today within each task's lead time | — |
| Completed | done on `day` | done on `day` | done on `day` |

- **Due on `day`**: a one-time open task whose trigger is `day`; a
  recurring task that `OccursOn(day)` and has `day` unticked. On today this
  is exactly `Select`'s non-overdue entries, so the Today rule does not
  move. On a future day the lead time does not filter — the user asked for
  that day.
- **Scheduled** orders by `Time.Start`, then name; **AnyTime** keeps
  `Select`'s order. Overdue entries keep their time in `TodayEntry.Time` but
  stay in Overdue.
- **ComingUp** is today's old Tomorrow and Upcoming merged: each one-time
  task at its trigger, each recurring task at its next unticked occurrence,
  only while `LeadTime.Shows` (default one week). Starred-ahead tasks stay
  out of it, as today. It is empty on every other day.
- **Completed**: `CompletedOn(task, day, zone)` as today.
- Unchanged: recurring tasks are never overdue; "today" is the user's zone;
  the star never puts a task on Today; `Select` and `GET /api/today` keep
  their meaning.

## 3. Task panel

`TimeRow.razor` in `Components/`, a property row under Due:

- Shown when Due/Until is set or the task repeats (the Remind me
  condition). Remind me stays, below it, and its hint now reads "How early
  the task shows in Coming up on My Day. On its due date it moves to Today
  as usual."
- Two `MudTimePicker`s, **Start** and **End**, 24-hour, outlined, dense.
  End is disabled until Start has a value. A clear button clears both.
- End ≤ Start shows "End must be after start" under the row and sends
  nothing.
- Edit mode sends `SetTaskTime` on each valid change. Add mode keeps it in
  `TaskDraft.Time`, sent after `SetTaskDueDate` in `CreateOnceAsync`.

## 4. My Day page

Canvas artboards: Desktop, Phone today / future / past.

**Header.** `PageHeading Title="My Day"`, then at the right: ‹ icon button,
a date button, › icon button. The date button reads "Today", "Tomorrow" or
"Yesterday", else `ddd d MMM`, followed by the muted date (`· Thu 8 Oct`),
and opens a `MudDatePicker` (picker variant `Dialog` below `md`, `Inline`
popover at `md`+) whose actions are **Today** and **Cancel**. Picking a day
closes it. On desktop a text **Today** button appears beside the arrows when
the day is not today; the phone top bar gets no Today button — the
picker's Today action is the way back. Below `md` the arrows and date sit in
a full-width bar under `MobileTopBar`, the date as a tonal pill.

**URL.** `/?day=yyyy-MM-dd`; `/` is today. A malformed `day` falls back to
today. Opening a task keeps `day` (`?day=…&task=…`), and closing the panel
keeps it too. Arrows and the picker navigate with `replace: false`, so Back
steps through visited days.

**Sections**, top to bottom, each hidden when empty:

1. **Overdue** — `Color.Error` heading, today only.
2. **Schedule** — `Color.Primary` heading. One column, max 760px on
   desktop: a time gutter (desktop `09:30–11:00` on one line, 96px; phone
   start over end, 52px) then the `TaskRow` card. Overlapping tasks simply
   follow each other.
3. **{Day}, any time** — "Today, any time", "Tomorrow, any time",
   "Fri 9 Oct, any time": the untimed tasks in the card grid. When Schedule
   and this section are both empty (and Overdue on today), this section
   stays with "Nothing planned for today." / "…for Fri 9 Oct."
4. **Starred** — heading with a muted "· when you have time"; card grid.
5. **Coming up** — today only, card grid grouped under a muted caption per
   day (`DueDateRow.Describe`), shown open, not collapsed.
6. **Completed (N)** — a collapsed `MudExpansionPanel` on today and future
   days; on a past day it is the only section, shown open as a plain
   section with "Nothing completed on Wed 7 Oct." when empty.

**Goals in progress** is removed; `GoalSummaryCard` stays for the goals
screens.

**Ticking.** A recurring row ticks the occurrence on the day shown
(Coming up rows: their own day, as now). A one-time row completes now; a
future task ticked from its day lands in today's Completed.

**Sync.** The page re-plans on `SyncRevision` and on day change; the
sidebar's My Day count stays today's `Select`.

## 5. TaskRow and sidebar

- `TaskRow` shows `Time` first in the meta line, `Color.Primary`, medium
  weight: `09:30–11:00`, or `09:30` without an end. Untimed rows are
  unchanged. Inside Schedule the gutter already shows it, so the row there
  hides it (`ShowTime="false"`).
- `NavSidebar` keeps its items and order (`ui-spec.md` §5) and takes the
  canvas look, still `MudNavMenu`/`MudNavLink`, styled through a
  `pspad-nav` class:
  - 12px padding inside the drawer, links with an 8px radius and 2px gap;
  - the active link filled with the primary at ~12% (`color-mix` on
    `--mud-palette-primary`, so every accent and dark mode follow) and the
    primary text at medium weight;
  - counts as a right-aligned muted number, not `My Day  (9)` in the
    label;
  - a muted "Areas" caption above the areas group;
  - outlined icons (`Icons.Material.Outlined.*`).
  Contrast holds the 4.5:1 / 3:1 rule of `ui-spec.md` §4 for every preset.

## 6. Docs

- `docs/src/pages/features.astro`: "One Today screen" becomes the day
  picker, schedule by time, any time, starred, coming up, finished that day.
- `docs/src/pages/index.astro`: the same in the feature line and
  description.
- No self-hoster change: no compose, env or port.

## 7. Tests

- Unit (`PSPad.Module.Tasks.Tests`): `TaskTime.Of` bounds; `Decide` for
  `SetTaskTime` (set, clear, unchanged, member grant, deleted task);
  `TodayRule.Plan` per row of the §2.2 table — past/today/future, timed vs
  untimed, recurring on a future day, lead time ignored on a future day,
  Coming up only on today, starred visibility, Scheduled order; existing
  `DayPlanTests`/`LeadTimePlanTests` move from Tomorrow/Upcoming to
  ComingUp.
- Integration (`PSPad.Api.Tests`): `TaskTime` survives MongoDB
  (`TimePersistenceTests`, like `LeadTimePersistenceTests`) and
  `GET /api/today` carries `time`.
- bUnit (`PSPad.App.Tests`): `TimeRow` (end disabled, invalid end, clear);
  `TaskDetailPanel` add flow sends `SetTaskTime`; `Today` page — sections
  per day kind, day from `?day=`, arrows, Goals gone, `?task=` keeps `day`;
  `TaskRow` time label; `NavSidebar` count and Areas caption.

## 8. Wrap-up (last plan task)

- Fold §2 into `specs/backend-spec.md` (Today rule bullets, lead time now
  "Coming up", `TaskTime`), §3–§5 into `specs/ui-spec.md` (My Day sections,
  panel rows, `TaskRow`, sidebar in §5, the masonry "Applied on" list), and
  update `specs/recurrence-lead-time-design.md` where it says Tomorrow and
  Upcoming.
- Delete this file and drop it from `AGENTS.md` §3 if it was added there.
