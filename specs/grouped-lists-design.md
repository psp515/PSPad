# Grouped task, inbox and goal lists

Issue #179. Status: implemented, revised after review on desktop.

## Problem

Every task, inbox item and goal was its own outlined `MudPaper` in `MudGrid Spacing="4"`, and
`.pspad-row` had a fixed `height: 60px`. A task cost about 78px on a phone (60 row + 2 border +
16 gap), so 5–6 items fit on a screen. The goals page showed about one active goal per screen,
because each card previewed up to five task rows.

The first cut capped every group at 760px in one column, which wasted most of a desktop screen.
Its divider started under the checkbox instead of at the name, the Add row stacked its icon over
its label, and the area-card rows sat flush against the card border.

## Decision

One outlined surface per section holds its rows, separated by 1px dividers, and rows size to their
content. This reverses the earlier "each task is its own card, never rows inside one shared paper"
rule for My Day. It applies to My Day, Inbox, list, goals and goal pages. The area board keeps its
list cards and row layout and only gains dividers between rows, because area rows carry many
details (due date, steps, recurrence, notes, list name).

On wide screens a group spans the content width and its rows flow into columns (list, inbox, goal
tasks, goals). My Day becomes two panes on desktop: the task sections on the left, the Schedule as
a vertical timeline on the right; on a phone the same two parts are tabs under the week strip.
Coming up is no longer on the page. No domain, command, sync or package change, so no ADR; the
lead-time rule (`adr/0053`) still runs and still decides Today membership for recurring tasks.

## Building blocks

- `Components/RowGroup`: `MudPaper` outlined, `pspad-row-group`, 12px radius, no max width.
  `Columns="true"` adds `pspad-row-group-cols`: a CSS grid of 2 columns from `md` (960px) and 3 from
  `xl` (1920px), 1px column gap. In a column group every row draws its own bottom divider
  (`::after`, hidden on `:last-child`) and a 1px hairline on its left edge (`box-shadow`, clipped
  by the group on the first column), so the first row of a second column never gets a stray line on
  top and rows in one grid row share a height. Subheads and `AddRow` span every column
  (`grid-column: 1 / -1`). A plain group keeps the `+ .pspad-row::before` top divider.
- Dividers are inset to the name's left edge: 55px for task and reference rows (4px row padding,
  3px stripe, 8px gap, 32px dense checkbox, 8px gap), 48px for goal rows.
- `TaskRow`: `min-height: 44px` (54px with `pspad-row-has-meta`); 4px padding on both sides;
  priority is a 3px stripe (`pspad-priority pspad-priority-high|medium`, `role="img"`, labelled
  "{Priority} priority") or a `pspad-priority-slot` spacer; unstarred stars show only on hover or
  focus under `@media (hover: hover)`. `ReferenceRow` gets the same spacer, padding and height.
- `pspad-row-ledger` on a group (My Day sections): from `md` the row's meta line sits on the name's
  line in a fixed 280px column at the right, so every row is one 44px line with aligned details.
- `AddRow`: one line (`d-flex align-center`), a `pspad-priority-slot`, a 32px icon box under the
  checkboxes, then the primary label ("Add task" / "Add item"). Keyboard operable.
- `pspad-row-subhead`: 11px uppercase label inside a group (Inbox days).
- Completed: `MudExpansionPanels` with `pspad-completed` (flat, chevron before the text,
  rotates when open), header `Completed · N`, collapsed by default.
- `RowSkeleton` is one `RowGroup` of 44px rows.
- `Components/DayTimeline`: the Schedule as a vertical timeline. Hours run from the floor of the
  first start to the ceiling of the last end (an end before its start, overnight, counts as 24:00;
  a task with no end gets 30 minutes), 56px per hour, an hour label gutter of 52px, a dashed
  half-hour line. Each task is an absolutely placed block (`pspad-timeline-task`, `pspad-tint`
  background, 3px primary left border, `pspad-timeline-task-done` muted when ticked) holding the
  checkbox, the time (`pspad-schedule-time` with `pspad-schedule-start`, `-dash`, `-end`,
  `-nextday`), the name and the list name; blocks under 40px go to one line. Overlapping tasks
  share the width in lanes. On today a red line (`pspad-timeline-now`) marks the current time in
  the user's zone.

## Pages

- **ListPage**: one `RowGroup Columns` of open tasks (or reference items) closed by an `AddRow`;
  Completed below in a column group. FAB menu unchanged (ADR 0033).
- **GoalPage**: a summary `MudPaper` (`pspad-goal-summary-card`: status chip, "Due … · N days left",
  progress bar, "{done} of {total} done" or "No tasks linked"), then the charts side by side, then a
  `SectionHeading` "Tasks" with the open count, a `RowGroup Columns` of open tasks, then Completed.
- **GoalsPage**: one `RowGroup Columns` per status; `GoalListRow` shows a status icon, name, due text
  ("Due 3 Nov" / "Overdue · 30 Sep" in error colour), a 4px progress bar and "done/total", or "N of M
  tasks done" muted for closed goals. The `⋯` menu keeps Mark achieved / Mark not achieved / Edit /
  Delete and stops click propagation. The five-task preview moved to the goal page.
- **Today (My Day)**: `pspad-day` holds `pspad-day-lists` (Overdue, Starred, {Day} any time, each a
  `RowGroup` with `pspad-row-ledger`, then Completed) and `pspad-day-schedule` (the `DayTimeline`,
  rendered only when the plan has Scheduled entries). From `md` the two are a grid, lists then a
  380px schedule column. Below `md` a tab bar (`pspad-day-tabs`: "Tasks", "Schedule · N" with the
  pending count) under the week strip picks which one shows; the bar exists only when there is a
  schedule, and `pspad-day-tab-tasks` / `pspad-day-tab-schedule` on `pspad-day` says which. The
  page starts on Tasks. Section order: Overdue, Starred, any time. Coming up is gone; membership,
  order and ticking rules are otherwise untouched (`TodayRule.Plan`, ADR 0035/0053). The lead time
  a task carries still only affects Coming up, so the panel's "Remind me" row has no visible effect
  until reminders ship.
- **InboxPage**: `InboxItemRow` in one `RowGroup Columns`, two-line clamp, time `HH:mm`
  right-aligned, items grouped by captured day **in the user's stored time zone** under
  `pspad-row-subhead`s, subtitle "N to process". The FAB captures; there is no capture row.
- **AreaBoard** (`ThingCard`): body gets `pspad-card-body` and the same inset divider between rows;
  rows get the same 4px side padding as everywhere else.

## Tests

bUnit tests assert the markup contracts above (`RowGroupTests`, `AddRowTests`, `DayTimelineTests`,
`GoalListRow` via `GoalsPageTests`, `InboxItemRowTests`, `TaskRowTests`, My Day panes and tabs,
goal page order) and `StylesheetRulesTests` pins the dividers, the column grid, the ledger meta
column, the hover-only star, the min-height and keyboard focus.
