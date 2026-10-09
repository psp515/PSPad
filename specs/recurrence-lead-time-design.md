# Yearly repeats, lead time and one-time vs repeating — design

Status: Built. Decision of record:
[ADR-0053](../adr/0053-yearly-repeats-and-lead-time.md). Issue #100.
Since #112 Tomorrow and Upcoming are one **Coming up** section on today's
My Day (`ui-spec.md`).

Three asks from one issue:

1. A repeat can fire **every year** ("Car insurance, every year on 15 Mar").
2. A task can say **how far ahead it shows** ("remind me 2 weeks before").
3. Adding a task separates a **one-time** task (a due date) from a
   **repeating** one (a rule and an end), instead of showing both rows side
   by side.

## 1. Scope

In:

- `RecurrenceKind.Yearly`, with the existing interval ("every 2 years").
- `LeadTime` on `TodoTask` — one-time and repeating tasks alike — and a
  `SetTaskLeadTime` command.
- `TodayRule.Plan` reading the lead time as the task's look-ahead for
  Tomorrow and Upcoming.
- A **One-time | Repeating** toggle in the task panel (Add and Edit), and a
  **Remind me** property row.

Out:

- Push notifications, e-mail, anything that fires while the app is closed.
  That is the Reminders subsystem (AGENTS.md §2, #8); the stored lead time is
  what it will read later.
- A lead time that puts a task on **Today** or **Overdue** early. The lead
  time only widens (or narrows) what Tomorrow and Upcoming show.
- A separate aggregate for repeating tasks. Both kinds stay `TodoTask`
  (`adr/0007`, `adr/0043`).
- Yearly on "the last Friday of March" or other positional rules.

## 2. Domain

### 2.1 Yearly kind

`RecurrenceKind.Yearly = 3`. `RecurrenceRule.Yearly(startsOn)` builds it; no
new field. The rule fires on `StartsOn`'s month and day, in every Nth year
counted from `StartsOn`'s year:

```
day.Month == StartsOn.Month
&& day.Day == min(StartsOn.Day, DaysInMonth(day.Year, day.Month))
&& (day.Year - StartsOn.Year) % Every == 0
```

A rule started on 29 Feb fires on 28 Feb in non-leap years. `EveryNth`
keeps its 1–99 bound. `Days` is empty and `DayOfMonth` is 0 for this kind,
as for Daily.

### 2.2 Lead time

```csharp
public enum LeadUnit { Days = 0, Weeks = 1, Months = 2 }

public sealed record LeadTime(int Amount, LeadUnit Unit)
{
    public DateOnly FirstShownFor(DateOnly day);
}
```

- `LeadTime.Of(amount, unit)` rejects an amount outside 1–99 with
  `DomainRejectedException("A lead time must be between 1 and 99.")`.
- `FirstShownFor(day)` is `day.AddDays(-n)`, `day.AddDays(-7n)` or
  `day.AddMonths(-n)`.
- It lives in `Tasks/` beside `TodoTask` (it is not part of the rule: a
  one-time task has one too).

`TodoTask.LeadTime` (nullable, `[JsonInclude]`). Documents stored before the
field exist read `null`, which means the default look-ahead.

Command `SetTaskLeadTime(CommandId, UserId, TaskId, LeadTime? LeadTime)`,
event `TaskLeadTimeSet(..., LeadTime? LeadTime)`, handler
`SetTaskLeadTimeHandler`, all in `Tasks/SetLeadTime/`. `Decide` rejects an
amount outside 1–99 (the handler may receive a hand-built record), returns
no event when unchanged. `null` clears it. A lead time on a task with no
date and no repeat is accepted and has no effect.

Statistics ignores `TaskLeadTimeSet`.

### 2.3 Look-ahead in `TodayRule.Plan`

A task's **look-ahead** is its `LeadTime`, or 7 days when it has none
(`UpcomingDays`, unchanged). A dated day `d` is **in view** on `today` when
`today < d` and `today >= FirstShownFor(d)`.

- One-time: the earliest trigger (due date or next unchecked step, as
  today) goes to Tomorrow when it is tomorrow, else to Upcoming, **only when
  in view**. A 1-day lead hides a task due in 3 days; a 1-month lead shows a
  task due in 3 weeks.
- Repeating: the next unticked occurrence after today that is in view.
  The scan runs from tomorrow to the furthest day the look-ahead can reach
  (`FirstShownFor(d) <= today`), stopping at the first occurrence. A yearly
  repeat on 15 Mar with a 1-month lead shows in Upcoming from 15 Feb.
- Overdue, Today, Starred and Completed do not read the lead time. A
  repeating task is still never overdue; a one-time task still becomes
  Today on its date.
- Upcoming is no longer bounded at `today + 7`: its end is per task.
  Upcoming's day captions already name the date.

## 3. UI

### 3.1 Kind toggle

`TaskDetailPanel` gets a `MudToggleGroup<bool>` (`One-time` = false /
`Repeating` = true, `pspad-task-kind`) above the property rows, in Add and
Edit. Its value is whether the task (or the draft) has a rule — no stored
kind, no client enum.

- One-time shows **Due** (`DueDateRow`, label "Due").
- Repeating shows **Repeat** (`RecurrenceEditor`) then **Until**
  (`DueDateRow`, label "Until", "No end date", never red).
- Both show **Remind me** (`LeadTimeRow`), then Priority, Goal, List.

Switching:

- One-time → Repeating sets `RecurrenceRule.Daily(today)` and clears the
  due date (a due date is not an end). Sent as `SetTaskRecurrence` then
  `SetTaskDueDate(null)`; in Add, the draft changes and nothing is sent.
- Repeating → One-time sends `SetTaskRecurrence(null)` and clears Until.
  the repeat's done days are dropped, as they are today when a repeat is
  removed; Statistics records keep the history.

The Repeat menu drops **Never** and its row loses the clear ✕: the toggle
is how a repeat ends being a repeat.

### 3.2 Repeat row

Presets gain **Yearly on 30 Sep** (today's month and day) after Monthly.
Custom's unit select gains **years**; with years it shows no weekday chips
and no day field — Starts picks the date. Descriptions: "Yearly on 15 Mar",
"Every 2 years on 15 Mar".

### 3.3 Remind me row

`Components/LeadTimeRow.razor`, a `PropertyRow` (`pspad-task-lead`, icon
`NotificationsNone`, label "Remind me"):

- Presets: 1 day, 3 days, 1 week, 2 weeks, 1 month, then Custom…
- Custom… opens a `MudDialog`: [1–99] [days | weeks | months], Save
  disabled until valid.
- Empty reads "1 week before" in the muted colour (the default look-ahead)
  — no ✕; a set value reads "3 days before", "1 month before", "2 weeks
  before" and carries a ✕ that clears it.
- Add: the draft's lead time is sent as `SetTaskLeadTime` after the due
  date.

## 4. Records and docs

- `adr/0053` (Proposed until built, then Active) and its index row; amends
  `adr/0043`.
- `specs/backend-spec.md` §4 (yearly kind, lead time, look-ahead) and the
  `todotasks` field list; `specs/ui-spec.md` task panel and My Day
  Upcoming paragraphs.
- `docs/src/pages/features.astro` and the landing page mention yearly
  repeats and lead time.

## 5. Tests

Unit (`PSPad.Module.Tasks.Tests`):

- `RecurrenceRule.Yearly`: fires on its day, not the day before; every 2
  years skips odd years; 29 Feb start fires 28 Feb 2027 and 29 Feb 2028;
  nothing before `StartsOn`; `TodoTask.OccursOn` still cuts at `DueOn`.
- `LeadTime`: bounds; `FirstShownFor` for each unit, including a month
  subtraction that clamps (31 Mar − 1 month = 28/29 Feb).
- `Decide(SetTaskLeadTime)`: sets, clears, no-op when equal, rejects 0 and
  100, rejects missing task.
- `TodayRule.Plan`: one-time with no lead (unchanged 7 days); 1-day lead
  hides day+3; 1-month lead shows day+20 in Upcoming; lead never moves a
  task into Today or Overdue; yearly repeat with 1-month lead shows its
  next occurrence 30 days out, and not 40 days out; repeat with 1-day lead
  shows only tomorrow's occurrence.

bUnit (`PSPad.App.Tests`): toggle shows the right rows; switching sends the
listed commands; Add sends `SetTaskLeadTime`; `LeadTimeRow` presets,
describe and custom dialog; `RecurrenceEditor` yearly preset, custom years,
describe strings, no Never item.

Integration (`PSPad.Api.Tests`): `SetTaskLeadTime` through the command
endpoint persists and syncs back (`leadTime` in the document).
