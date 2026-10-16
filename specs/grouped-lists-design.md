# Grouped task, inbox and goal lists

Issue #179. Status: implemented.

## Problem

Every task, inbox item and goal was its own outlined `MudPaper` in `MudGrid Spacing="4"`, and
`.pspad-row` had a fixed `height: 60px`. A task cost about 78px on a phone (60 row + 2 border +
16 gap), so 5–6 items fit on a screen. The goals page showed about one active goal per screen,
because each card previewed up to five task rows.

## Decision

One outlined surface per section holds its rows, separated by 1px dividers, and rows size to their
content. This reverses the earlier "each task is its own card, never rows inside one shared paper"
rule for My Day. It applies to My Day, Inbox, list, goals and goal pages. The area board keeps its
list cards and row layout and only gains dividers between rows, because area rows carry many
details (due date, steps, recurrence, notes, list name). No domain, command, sync or package change,
so no ADR.

## Building blocks

- `Components/RowGroup`: `MudPaper` outlined, `pspad-row-group`, 12px radius, max 760px, dividers
  from `.pspad-row + .pspad-row::before` inset 44px. One column on every width: dividers across grid
  columns read badly, and the Schedule already used 760px.
- `TaskRow`: `min-height: 44px` (54px with `pspad-row-has-meta`); priority is a 3px stripe
  (`pspad-priority pspad-priority-high|medium`, `role="img"`, labelled "{Priority} priority") or a
  `pspad-priority-slot` spacer; unstarred stars show only on hover or focus under `@media (hover: hover)`;
  optional `Lead` fragment rendered before the checkbox. `ReferenceRow` gets the same spacer and height.
- `AddRow`: full-width primary "Add task" / "Add item" / "Capture a thought" row, keyboard operable.
- `pspad-row-subhead`: 11px uppercase label inside a group (Coming up days, Inbox days).
- Completed: `MudExpansionPanels` with `pspad-completed` (flat, chevron before the text,
  rotates when open), header `Completed · N`, collapsed by default.
- `RowSkeleton` is one `RowGroup` of 44px rows.

## Pages

- **ListPage**: one `RowGroup` of open tasks (or reference items) closed by an `AddRow`; Completed below.
  FAB menu unchanged (ADR 0033).
- **GoalPage**: a summary `MudPaper` (`pspad-goal-summary-card`: status chip, "Due … · N days left",
  progress bar, "{done} of {total} done" or "No tasks linked"), then a `RowGroup` of open tasks,
  Completed, then the unchanged charts.
- **GoalsPage**: one `RowGroup` per status; `GoalListRow` shows a status icon, name, due text
  ("Due 3 Nov" / "Overdue · 30 Sep" in error colour), a 4px progress bar and "done/total", or "N of M
  tasks done" muted for closed goals. The `⋯` menu keeps Mark achieved / Mark not achieved / Edit /
  Delete and stops click propagation. The five-task preview moved to the goal page. `GoalCard` and
  `GoalSummaryCard` were deleted.
- **Today (My Day)**: Overdue, any time and Starred are one `RowGroup` each. The Schedule is one
  group with the time gutter inside the row (132px desktop, 52px below `md`, divider inset 12px).
  Coming up is one group with a `pspad-row-subhead` per day. Membership, order and ticking rules are
  untouched (`TodayRule.Plan`, ADR 0035/0053).
- **InboxPage**: `InboxItemRow` (renamed from `InboxItemCard`) in one `RowGroup`, two-line clamp, time
  `HH:mm` right-aligned, a "Capture a thought" `AddRow` on top, items grouped by captured day **in the
  user's stored time zone** under `pspad-row-subhead`s, subtitle "N to process".
- **AreaBoard** (`ThingCard`): body gets `pspad-card-body` and the same divider between rows.

## Tests

bUnit tests assert the markup contracts above (`RowGroupTests`, `GoalListRow` via `GoalsPageTests`,
`InboxItemRowTests`, `TaskRowTests`, My Day schedule and Coming up groups, goal summary) and
`StylesheetRulesTests` pins the divider, the hover-only star, the min-height and keyboard focus.
