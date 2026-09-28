# UI Spec

Standing rules for building `PSPad.App` screens so every page looks and
behaves like it belongs to the same product. This is a rulebook, not a
history — it states what the client does now and how to extend it
consistently. For *why* a given rule exists, the superseded design
narratives (`ui-ux-redesign-design.md`, `ui-redesign-2-design.md`,
`ui-polish-design.md`, `statistics-design.md`) and the ADRs they cite are in
git history (`git log -- specs/`); the ADRs themselves stay in `adr/` and
remain the decision record where a rule traces back to one.

Where this spec and an ADR disagree, the ADR wins. Where this spec and the
code disagree, say so rather than silently following either.

---

## 1. Component choice

**Reach for a MudBlazor component before writing custom markup or a new
`pspad-*` CSS class.** A custom class is for things MudBlazor genuinely has
no component for — the brand mark, page-specific chrome — never a
substitute for a component that already exists.

| Need | Use | Not |
|---|---|---|
| A responsive card/row grid | `MudGrid` + `MudItem` | a hand-rolled CSS `display: grid` class |
| A bordered/elevated container (card, panel, row group) | `MudPaper` | a `<div>` with a custom border class |
| A button, icon button, floating action button | `MudButton` / `MudIconButton` / `MudFab` | a styled `<button>` |
| A dropdown of actions on one item | `MudMenu` + `MudMenuItem` | a custom popover |
| A modal confirmation or input | `MudDialog` via `IDialogService` | a hand-rolled overlay |
| A form field | `MudTextField` / `MudSelect` / `MudCheckBox` | a bare `<input>` |
| A loading placeholder | `MudSkeleton`, wrapped in `MudPaper` where the real content has a border | an empty `<div>` |
| Vertical/horizontal flex spacing | `MudStack`, or `d-flex`/`gap-*` utility classes | inline `style` margins |
| A chart | `MudChart` | a third-party charting library |
| A calendar-style heatmap (consistency) | a custom `pspad-heatmap` CSS grid | `MudChart`'s `ChartType.HeatMap` |
| A section that opens on click (the record feed) | `MudExpansionPanels` + `MudExpansionPanel` | a hand-rolled show/hide flag |

Before adding a new `pspad-*` class, check this table first. If nothing
fits, the class is legitimate — but it means the styling is specific to
PSPad's brand or a one-off page need, not a generic layout or component
problem MudBlazor already solves.

Not every `pspad-*` class carries a CSS rule. Several exist purely as a
stable selector for tests to find a MudBlazor element that has no other
reliable hook (`pspad-sign-out`, `pspad-account-card`, `pspad-delete-account`,
`pspad-confirm-email`, `pspad-confirm-delete`). Don't "clean up" an
apparently-unstyled `pspad-*` class without checking whether a test depends
on it first.

**The one exception on record: the statistics screen's Consistency
heatmap** (`Statistics/ConsistencyHeatmap.razor` — one cell per day — over
`.pspad-heatmap*` in `wwwroot/css/app.css`). MudBlazor ships
`ChartType.HeatMap`, but it is a generic matrix chart — it gives no
per-cell `title`/`aria-label`, and without one the grid is a wall of
coloured squares a screen reader cannot read. A hand-rolled CSS grid earns
its place here for exactly the reason the table above says a custom class
ever can: MudBlazor genuinely has no component that does this.

It sits beside the Inbox-captures panel at `md` and up (`MudItem xs="12"
md="6"` each, matching the chart grid above them) and both stack to full
width below that breakpoint — the two still belong together conceptually,
even though only one of them is a `pspad-heatmap` grid (below). The
heatmap grid is a flowing `.pspad-heatmap-flow` (`display: grid;
grid-template-columns: repeat(auto-fill, minmax(30px, 44px))`) — cells wrap
to as many rows as the range needs, deliberately giving up day-of-week
alignment (the maintainer's call, 2026-09-25: "it doesn't have to reflect
how the month has days, it can just be squares that auto-fit to space") in
exchange for actually using the panel's width instead of leaving a
GitHub-style calendar stranded in one corner. `--pspad-heatmap-cell-min`
(30px) is the floor that keeps a long range legible instead of one row per
day; `--pspad-heatmap-cell-cap` (44px) is the ceiling. Both bounds live on
the track, and the track is deliberately **not** `1fr`: with month sections
(below) a grid can hold as few as one cell, and an `fr` track would hand
that one cell a quarter of the panel and strand it in the middle of its own
row — the same "spread and dead space" failure this design exists to avoid.
A bounded track keeps every square the same size whatever the month is
worth, at the cost of a little unused width where the last column does not
divide evenly into the panel.

**Month sections beyond 30 days, one grid each.** Above 30 cells the
component renders one `.pspad-heatmap-flow` **per month**, each inside a
`.pspad-heatmap-month` wrapper headed by a `.pspad-heatmap-month-label`
(`MMM yyyy`, uppercased by CSS). At 30 days or fewer it stays a single grid
with no heading: one month label over one short grid is noise, not
orientation. Separate grids are what makes this work at all — the earlier
attempt put the label *inside* one grid as a `grid-column: 1 / -1` row, which
forces every column track to register as "used", defeats `auto-fit`'s collapse
and leaves a short trailing group stranded at the minimum cell size with dead
space beside it. A grid per month has nothing to collapse around: each one
`auto-fit`s to the panel on its own, and a 24-day month simply fills fewer
rows than a 31-day one. Cost: at 365 days there are thirteen headings and
thirteen grids, so the panel is taller than one continuous flow would be —
accepted, because "which month am I looking at" was unanswerable before.
Each Sunday cell still carries a 3px accent border on its top edge
(`--mud-palette-secondary`) — the only left-to-right cue for where a week
starts, now that weekday rows are gone. Each cell also renders its
day-of-month number (`aria-hidden`, the accessible label is unaffected); text
colour switches from `--mud-palette-text-secondary` to the theme's computed
`--mud-palette-primary-text` at `data-level="3"`/`"4"` so it stays legible
against both the near-background and fully-saturated ends of the shade ramp.
The heatmap ends in a `HeatmapKey` — the conventional five-step ramp plus one
sentence saying what darker means. `HeatmapLevel.Of(count, maximum)` is the
shared five-step scale; a key's swatches deliberately do not carry the
`pspad-heatmap-cell` class, so they are never mistaken for data cells. Every
data cell keeps its `title`/`aria-label` (`2026-09-24: 3 completed`), which is
the accessibility reason the custom grid exists at all.

**A cell is a `<button>`, and a tap reads out its day and count.** `title` is
a hover tooltip: it never fires on touch, so on a phone the count in it was
unreachable — the maintainer reported exactly that. Each data cell is
therefore a `<button type="button">` that sets the selected day on click
(tapping it again clears it), and a `.pspad-heatmap-readout` line between the
grids and the key shows `24 Sep 2026 — 3 completed`, or the hint *"Tap a
square for the day and its count."* when nothing is selected. It is
`aria-live="polite"`, so a screen reader hears the change; the selected cell
also carries `data-selected="true"` (a 2px `--mud-palette-secondary` ring)
and `aria-pressed`. The readout keeps its line height when empty, so a tap
never shifts the grid under the finger that made it, and it is
`position: sticky; bottom: 0` **only while a day is selected**
(`[data-selected="true"]` on the readout itself): a year of month sections
is taller than a phone screen, so an answer left at the bottom of the panel
is an answer nobody sees — but a permanently pinned bar would cover a strip
of squares forever, so the idle hint stays in the flow and only an answered
readout floats. `title` stays on every
cell — this is an addition for touch, not a replacement for hover — and so
does `aria-label`. `MudTooltip` was the first thing considered and rejected:
`ShowOnClick` does exist in MudBlazor 9.9.0 (verified against the shipped
assembly's XML docs), but it wraps each child in its own `mud-tooltip-root`
element, which would take the cell's place as the grid item and break the
`auto-fit` sizing the grid depends on, and it would mean up to 365
`MudPopover`s registered with the popover provider for one panel. A selection
readout is one line of state and no popovers.

**The Inbox panel is a `MudChart` bar chart, not a heatmap**
(`StatisticsPage.razor`, inline alongside the page's other charts, not a
separate component — five to fifty-three weekly values with a trend in
them is what a bar chart is for, where a heatmap only earns its keep on
dense data scanned for a pattern; changed 2026-09-25 after the maintainer
saw the original heatmap in dark mode, where a handful of low counts all
landed in the bottom shade buckets and were indistinguishable from the
panel background). It is headed *"Captured into the Inbox each week"*: one
bar per week, height = **how many items were captured that week**, whatever
became of them afterwards (changed 2026-09-26 at the maintainer's request
from the backlog left at each week's end — see `adr/0039`). Built the same
way as the page's other three charts — see "Charts" below. `MudChart`
renders SVG with no per-bar accessible text, so the panel also carries a
`class="mud-sr-only"` (MudBlazor's own visually-hidden utility) `<table>` of
week/count pairs beside the chart, which is how a screen reader still gets
every week's number now that there is no per-cell `title`/`aria-label` to
read.

---

## 2. Layout & spacing

**Card/row grids.** Every place a list of cards or rows is drawn uses
`MudGrid Spacing="4"`, one `MudItem xs="12" sm="6" md="4" xl="3"` per item:

```razor
<MudGrid Spacing="4">
    @foreach (var item in items)
    {
        <MudItem xs="12" sm="6" md="4" xl="3">
            <Card ... />
        </MudItem>
    }
</MudGrid>
```

One column on a phone, two on a tablet, three from `md`, four once the
viewport passes MudBlazor's `xl` breakpoint (1920px). `sm`/`md`/`xl` (600px/960px/1920px) match
`BrowserViewport`'s own `Breakpoint.MdAndUp` split used for the sidebar, so
the grid and the shell agree on where "wide enough" starts. Row-flow, not
column-flow — DOM order stays reading order for keyboard and screen-reader
navigation.

Applied on: `AreaBoard` (list cards), `GoalsPage` (goal cards, active and
achieved separately), `Today` (overdue, today, starred, tomorrow, goals in progress,
completed and upcoming each as their own grid), `InboxPage`, `ListPage` (open and completed separately),
`SettingsPage` (Account, Application settings, Sync, Danger zone each their
own card), and both skeleton components (`RowSkeleton`, `CardSkeleton`).

**Spacing scale.**

| Use | Value |
|---|---|
| Grid gap between cards/rows | `Spacing="4"` on `MudGrid` |
| Card interior padding | `pa-3` |
| Row interior padding | `px-3 py-2` |
| Space below a page title | `mb-4` |
| Space between stacked sections | `mt-4` / `mb-4` |

**Containers.** Page content sits in the app's full-width
(`MaxWidth.False`) container, set once in `AppShell`, so it starts at the
sidebar's edge and uses the whole window instead of a centred column —
individual pages never set their own max width. The container
(`pspad-content`) carries 96px of bottom padding, the FAB's height plus its
inset, so the FAB never covers the last row.

**Sidebar breakpoint.** The sidebar is permanent at `md`+ (≥960px) and a
temporary drawer behind a hamburger below it, using MudBlazor's display
utilities (`d-none d-md-flex` / `d-md-none`), never `MudHidden` — `MudHidden`
resolves through `IBreakpointService`'s JS round trip and renders its
default branch before the first callback, flashing the wrong navigation on
load. Both branches live in the DOM at all times, separated only by CSS
resolved before first paint.

**Mobile app bar.** Below `md`, a dense `MudAppBar` carries only the
hamburger and `ConnectionStatus`. No account avatar — identity lives in the
sidebar's `AccountBadge`, one tap away behind the hamburger.

**New version prompt.** When a deployed build's service worker has installed
and is waiting, `AppShell` shows one `Severity.Info` snackbar — "A new version
of PSPad is available." — with a `Reload` action and a close icon. It needs
interaction and never times out. Reload activates the waiting worker and
reloads the page; closing it leaves the old version running until every tab
closes (`adr/0040`).

---

## 3. Page structure

**Every page gated on `_loaded`.** A page carries `bool _loaded`, set true
only at the end of its data reload. Until then it renders a skeleton
(`RowSkeleton` or `CardSkeleton`), never an empty state or a "Nothing here"
message — a screen must never show an empty state it has not verified.

**Title pattern.** A page's title is `<MudText Typo="Typo.h5" Color="Color.Primary" Class="mb-4">Title</MudText>`,
rendered both in the loading and loaded branches so nothing jumps on load.
A screen nested under another (a list under its area) puts a back
`MudIconButton` (`ArrowBack`, `Color.Primary`) to the left of its title,
linking to the parent screen.

**My Day sections.** Top to bottom: **Overdue** (`Color.Error` heading),
**Today**, **Starred**, **Tomorrow**, **Goals in progress**, then one `MudExpansionPanels`
holding **Completed (N)** and **Upcoming (N)**, both collapsed by default.
Every section hides when empty, except Today, which says "Nothing due
today." when Overdue is empty too. Each task is its own outlined
`MudPaper` card in a `MudItem`, sized to its content like the goal cards,
never rows inside one shared paper. Upcoming groups its rows under a muted
caption per day (`DueDateRow.Describe`). Membership comes from
`TodayRule.Plan`, never from the page. A recurring row ahead of today
ticks the occurrence on its own day, not today's. Goals in progress are
`GoalSummaryCard`s ordered by due date, undated last, and open the goal
screen `/goals/{id}`.

**Goal screen.** `/goals/{goalId}` mirrors the list screen: back arrow to
`/goals`, the goal's name as title with a small outlined status `MudChip`
(In progress / Achieved — `Color.Success` — / Not achieved) and its due
date as a caption under it. One `MudGrid` then holds the **Progress**
card, the **Pace** card (dated goals only) and every open task as its own
card (list name and added day on each row), so task cards flow up beside
the chart on wide screens; a collapsed **Completed (N)** follows. With no open tasks it shows an `EmptyState` "No open
tasks." with no create action — a task is created in a list, not a goal.
A goal card's name, its **Show all** link and every `GoalSummaryCard`
(achieved, not achieved, My Day) lead here, so a closed goal's tasks stay
reachable.

**Goal progress chart.** An outlined `MudPaper` in a half-width `MudItem`
(`xs="12" md="6"`) holding a `MudChart` line chart with two series: **On
the goal** (tasks created by the end of each week) and **Done** (of those,
completed by then), one point per Monday-start week in the user's time
zone, from the first task's week to the current week. `GoalProgress`
computes it; the page only draws. Recurring tasks are left out — they are
never completed as a whole — and a muted caption "Recurring tasks not
counted" says so when the goal has any. Deleted tasks are left out. A task
counts from its creation, not from when it was linked to the goal (the
link carries no time). With nothing to count the card is not rendered.
Axis ticks and labels come from `ChartAxis`, shared with Statistics.

**Goal pace card.** `GoalPaceCard`, in a `MudItem` (`xs="12" md="6"
xl="3"`) beside the progress chart, shown only when the goal has a due date
and something to count. Two large `MudProgressCircular` rings: **time** —
share of the span from the first counted week's Monday to the due date
already used, `Color.Primary`, captioned "N days left" / "Due today" /
"N days overdue" (ring and text `Color.Error` once overdue); **work** —
done ÷ counted tasks from the latest progress week, `Color.Success` when
work ≥ time, `Color.Warning` when behind, captioned "Work done · on pace"
/ "· behind" so the state never rides on colour alone. `GoalPace` computes
it from the `GoalProgress` weeks.

**Shared row/card components, never duplicated per screen.** One
`TaskRow` renders in My Day, list cards, the list screen and search
results. One `ListCard`, one `GoalCard`, one `InboxItemCard`. `ListCard` and
`GoalCard` are thin wrappers over one `ThingCard` — collapse arrow, title
link, open count, `⋯` menu, at most five open `TaskRow`s and a
**Show all (N)** link to the thing's own screen — and differ only in their
header extras and row caption: a goal card's rows name their list and the
day the task was added (`Added 12 Sep`, in the user's time zone). A single
component per concept means a rule like never-overdue-for-recurring-tasks
cannot drift between the screens that display it.

**Repeating rows.** `TaskRow` shows a repeating task's due date as
"Until 12 Oct", never red. A repeat past its Until (`TodoTask.EndedBy`)
renders checked; `ListPage` files it under Completed, list and goal cards
leave it out of their open tasks, and `GoalSummaryCard` counts it done. My
Day never shows it — it has no occurrence left. Every tick handler (My Day,
list screen, area board, goals, panel) toggles the occurrence and does
nothing on a day the task does not occur on (an off-day of "every 2 days",
or past its Until) unless that day is already ticked.

**Every repeated row or card carries `@key` on its entity id.** Ticking a
task moves it between sections, and without a key Blazor hands the vacated
row's component to the next task, whose `MudCheckBox` keeps its own ticked
state because its `Value` parameter did not change — two same-named tasks
then both look done (issue #44). Key on the id, never on the name.

**Task detail is an overlay, addressed by query string.** Clicking a task
appends `?task={taskId}` to the current route; `TaskDetailPanel` renders as
a slide-in overlay (full-screen below `md`) without reflowing the page. The
query string, not component state, so back-navigation closes the panel
without leaving the screen, and a task is linkable. Adding a task uses the
same panel in its new-task mode, addressed as `?task=new&list={listId}`
(`TaskQuery.ForNewTask`) — never an inline field or a dialog.

**Detail panels share one shell.** `Components/DetailPanel.razor` is the
only right-anchored detail drawer: 360px from `md` up, full width below it.
Below `md` the drawer's height and top follow `window.visualViewport`
(the `--pspad-visible-height`/`--pspad-visible-top` variables set in
`index.html`, with `interactive-widget=resizes-content` on the viewport
meta), so the on-screen keyboard shrinks the panel instead of covering its
footer — Add/Save stays reachable while typing. Its header row holds an X close button top-left, a title (`Typo.h5` from
`md` up, `Typo.h6` below) and a `HeaderActions` slot on the right for
toggles such as the star; the labelled name field sits under it (`Header`
slot), then scrolling content, then a footer pinned to the bottom of the
drawer (`Footer` slot plus an optional bar with Save on the left, filled
primary, and Delete on the right, text `Color.Error`). Dividers inside the
panel carry `flex-grow-0` — `MudDivider` grows by default and would
otherwise stretch into an empty band inside the flex column. Each
button renders only when its callback is bound. An existing thing's fields
save as they change — each edit is its own command, so there is no Save for
it. Save exists only while creating, where nothing is written until the
whole draft is committed.

**One task form for add, edit and view.** `TaskDetailPanel` is a single
component whose mode follows from its parameters — *Add* (`?task=new`),
*Edit* (an existing task), *View* (an existing task while the shell is not
ready: every control disabled). Modelled on Microsoft To Do's detail pane
and Todoist's task view: the task itself on top, its properties as
one-line rows under it, nothing boxed in a form. Top to bottom:

1. Header — X, the task's place as `Area › List` (`New task · Area › List`
   in Add) in `Typo.body2`, star on the right.
2. Done checkbox (outside Add) beside the name, an unboxed `Typo.h6`
   `MudTextField` with no underline or label.
3. **Steps** (outside Add) — `StepList`: `MudCheckBox` rows with a remove
   icon, then an unboxed "Add step"/"Next step" field (Enter adds).
4. Property rows, each a `PropertyRow` — icon · label · value, the whole
   row a `MudMenu` activator; an empty value reads in the muted text colour
   ("No due date", "Never", "No goal"), a clearable one carries a trailing
   ✕:
   - **Due** (`DueDateRow`) — on a repeating task labelled **Until**,
     empty as "No end date" and never red when past (`FlagsPast`). Today / Tomorrow / In 2 days / Next week (the
     next Monday), each with its date, then "Pick a date…" opening a
     `MudDialog` (`pa-0`) holding a static, flat, toolbar-less
     `MudDatePicker` so only one frame shows (never a hidden
     dialog-variant picker, whose overlay renders inside its `d-none` host
     and never shows); picking a day sets it and closes; the value reads relatively (Today, Tomorrow,
     Yesterday, `ddd, d MMM`), in `Color.Error` when overdue.
   - **Repeat** (`RecurrenceEditor`, Add included — the draft's rule is
     sent as `SetTaskRecurrence` after `CreateTask`, before the due date) —
     Daily, Weekdays, Weekly on today's weekday, Monthly on today's day,
     Custom…, Never. Presets start today. Custom… opens a `MudDialog`:
     Every [1–99] [days | weeks | months], weekday chips (weeks, Monday
     first), On day 1–31 (months), Starts (date picker, default the current
     rule's start or today); moving Starts moves the weekday chip or
     day-of-month default with it until the user has picked one; Save
     stays disabled until the fields are valid. Setting a repeat on a task
     whose due date falls before the rule's start clears that date (sent
     after `SetTaskRecurrence`), so the repeat does not start already
     ended. The value reads "Daily", "Weekly on Mon, Thu", "Every 3 weeks
     on Mon", "Every 2 days", "Every 6 months on day 15". A repeating task
     shows its last seven occurrences as chips under the row, then a muted
     tally caption: "Not done yet", "Done N times", or "Done N times · M in
     a row" (`RepeatTally`).
   - **Priority** — the four fixed levels with coloured dots.
   - **Goal** — the user's goals.
   - **List** (outside Add) — lists grouped under area headings; picking
     one moves the task at once. There is no Move button.
   Room for later task fields (note, reminders) goes under the rows.
5. Footer — "Created …" on the left, a red trash `MudIconButton` on the
   right that asks via `ConfirmDialog` before deleting. In Add the footer
   holds only **Add task**, and Enter in the name field adds too.

**Area and goal detail use the same shell.** `Layout/AreaDetailPanel.razor`
is addressed as `?area=new` (sidebar **+ New area**) or `?area={areaId}`
(the area's **Edit area** FAB item), via `AreaQuery`.
`Layout/GoalDetailPanel.razor` is addressed as `?goal=new` (the Goals FAB,
or the empty state) or `?goal={goalId}` (a goal card's **Rename**, or the goal screen's **Edit goal**), via `GoalQuery`. Both open with an outlined **Name** field
under the header. A goal adds a **Status** `MudSelect` (In progress /
Achieved / Not achieved, existing goals only) and a **Due** `DueDateRow`
(in add mode too). The Goals page shows in-progress goals as cards ordered
by due date, undated last. A card shows "Due …" on its own line under the
name, in `Color.Error` once the date has passed. A card's `⋯` menu offers
the two closing statuses.
- **Sections:** Achieved and Not achieved goals get their own always-visible
  sections below. Each section heading is a `Typo.h6` title with a muted
  count and a `MudDivider` under it, with no icons. An "In progress" heading
  appears once any goal is closed.
- **Summaries:** closed goals render as `GoalSummaryCard`, not `GoalCard`.
  It is an outlined paper with a status-coloured left accent (success or
  error) and the status icon, showing only the name and "N of M tasks
  done". It has no progress bar and no due date. The whole summary opens
  the goal screen. An in-progress goal (My Day only) gets a primary accent
  and a flag icon, and adds a "Due …" caption (`Color.Error` once passed,
  omitted when undated) and a thin `MudProgressLinear` when tasks are
  linked.
- New: **Add area** / **Add goal** sits on the left of the footer, and
  Enter also adds. A new area then opens its screen; a new goal closes the
  panel.
- Existing: fields save as they change, with no Save. **Delete area** /
  **Delete goal** sits on the right and asks via `ConfirmDialog` first.
  Deleting an area goes home; deleting a goal closes the panel.

**New lists use the same shell.** `Layout/ListDetailPanel.razor` is
addressed as `?list=new&inarea={areaId}` (`ListQuery.ForNewList`) from the
area's **New list** FAB item or its empty state — full screen below `md`, a
side panel from `md` up. It holds only an outlined **Name** field under a
`New list · Area` title, with **Add list** at the bottom left; Enter also
adds. The new list takes the next position in its area and the panel
closes, leaving the area board. Renaming a list still goes through
`NameDialog`.

Areas and goals are never created or renamed through `NameDialog`, and
lists are never created through it.

**Inbox items use the same shell.** `Layout/InboxItemPanel.razor` is
addressed as `?inbox=new` (the Inbox FAB or its empty state) or
`?inbox={itemId}` (tapping an item card), via `InboxQuery`.
- **Capture:** only a Name field, with **Add** at the bottom left; Enter
  also adds. The item keeps the time it was captured, which its card shows.
- **An existing item** opens as a convert-to-task form: Name, then
  `ListRow`, `DueDateRow`, `PriorityRow` and `GoalRow`, plus a star in the
  header. The Name field is the item's own text and saves as it commits
  (`RenameInboxItem`), so an item can be reworded without being converted.
  - **Convert to task** on the left sends `OrganiseInboxItem` and then only
    the edits that differ from the defaults, so one tap files a finished
    task.
  - **Discard** on the right removes the item.
  - The List row starts on the list used for the previous conversion
    (`AppState.LastInboxListId`), falling back to the first list.
- `PriorityRow`, `GoalRow` and `ListRow` are shared with `TaskDetailPanel`.
  `GoalRow` offers only In progress goals, but still names a linked goal
  that has since closed.
  The Inbox never uses a dialog to capture.

**Empty states share one component.** A page or board with no items yet
shows `Components/EmptyState.razor` as the first cell of its grid, sized
like one card (`MudItem xs="12" sm="6" md="4" xl="3"`): an outlined
`MudPaper` with a dashed border, an icon and a message. The whole card is
the create action — `role="button"`, focusable, Enter/Space or a click
starts creating the first item; no separate button. Only after `_loaded` —
see above.

**The statistics screen (`StatisticsPage`, `/statistics`) is server-rendered
data over plain REST, not the replica** — no IndexedDB, no outbox, no
command pipeline. It is its own layout, not the §2 card grid: a `MudGrid`
of tiles (`sm="3" xs="6"`), four charts (`md="6" xs="12"`), then the
Consistency heatmap and Inbox-captures chart side by side (`md="6" xs="12"`,
see §1) and the record feed full width below them. The feed is a
`MudExpansionPanel` ("Everything that happened") **collapsed by default** —
it is the raw log, not the summary someone opens the screen for — and the
page holds its loaded pages in its own state, so opening, paging with *load
older* and collapsing again never loses a record or breaks pagination. The last
successful payload is cached in `localStorage` keyed by payload shape and
range (`pspad.statistics.2.{days}` — the shape version is what stops an
overview cached before a new series existed from rendering as a null
series), rendered
immediately on load, then refreshed behind it — an *updated
&lt;relative time&gt;* stamp and an offline banner cover the gap when a
refresh fails. That cache is user data: it purges on the same
signed-in/owner-mismatch path ADR-0018 and ADR-0025 already purge the
replica and outbox on, so a second user on the same device never sees the
first user's numbers before the first fetch lands.

**Every page has a page-level action set** — creating the thing the page
is about, renaming or deleting that thing — distinct from the item-level
actions each card or row already carries (a list card's own `⋯`/`+`, a
goal card's achieve/reopen, an inbox item's open). A page's action set
drives exactly one bottom-right affordance, a bare `MudFab` or a `MudFab`
wrapped in a `MudMenu`, both pinned with the `pspad-fab` CSS class. Built
directly on each page, not through a shared component — each page's FAB is
a handful of lines specific to that page's own actions:

- **Zero actions** → no FAB.
- **One action** → a plain `MudFab`, performing the action directly
  (typically opening a dialog).
- **Two or more actions** → one `MudFab` opening a `MudMenu` ("FAB Menu")
  listing every action. Never a second FAB, never a header `⋯` competing
  with it.

FAB Menu items (`MudMenuItem`) are icon-only, wrapped in `MudTooltip` for
the accessible label — a visible text label is a last resort, only when no
icon reads unambiguously on its own.

| Page | Page-level actions | Result |
|---|---|---|
| Area | New list, Edit area (→ area panel), Delete area | FAB Menu |
| Goals | Add goal (→ new-goal panel) | plain `MudFab` |
| Goal | Edit goal (→ goal panel), Delete goal | FAB Menu |
| List | Add task (→ new-task panel), Rename list, Delete list | FAB Menu |
| Inbox | Capture (→ capture panel) | plain `MudFab` |
| My Day, Settings, Statistics | none | no FAB |

`+ New area` stays pinned in the sidebar — it is not a page's own action,
it belongs to the sidebar's area list. Item-level rename/delete stays on
the item's own `⋯` (`ThingMenu`) wherever the item is a card or row inside
a page, e.g. `ListCard`'s and `GoalCard`'s own menus on `AreaBoard` and
`GoalsPage` — those are untouched by the page-level rule above.

---

## 4. Visual & theming

**Palette.** Defined once in `Theme/PSPadTheme.cs`, built from MudBlazor's
Material colour constants (`Colors.Green.Darken3`, `Colors.Gray.Lighten4`, …),
never a hex literal in a component. Grounds are Material neutrals; only the
accent is coloured, so a user-picked accent never clashes with a tinted
surface:

| Token | Light | Dark |
|---|---|---|
| Primary | accent, Material 700–900 | accent, Material 200–300 |
| Secondary | accent, one step deeper | accent, Material 100 |
| Error | `Red.Darken2` | `Red.Lighten2` |
| Warning | `Orange.Darken4` | `Orange.Lighten2` |
| Success | `Green.Darken3` (never follows the accent) | `Green.Lighten2` |
| Background | `Gray.Lighten4` `#F5F5F5` | `#121212` |
| Surface, drawer | `#FFFFFF` | `#1E1E1E` |
| Appbar | `#FFFFFF` | `#272727` |
| Lines | `Gray.Darken1` | `Gray.Darken1` |
| TextPrimary / TextSecondary | `Gray.Darken4` / `Gray.Darken2` | `Gray.Lighten3` / `Gray.Lighten1` |

**Accent** is one of seven presets — Green (default), Teal, Blue, Indigo,
Purple, Pink, Orange — or a **custom** colour. `PSPadTheme.For(accent)` hands
back one cached `MudTheme` per preset; `PSPadTheme.ForCustom(hex)` derives the
four accent shades from the picked colour by moving only its HSL lightness —
deepened for light mode until it holds 4.5:1 on the background, lightened for
dark mode until dark text holds 4.5:1 on it — so the hue the user picked
survives and contrast still holds. `ThemePreference` builds the theme once per
change and the `MudThemeProvider` takes `ThemePreference.Theme`.
`PSPadThemeTests` checks every preset and a set of awkward custom colours
(yellow, white, black, grey, pure primaries) in both modes: 4.5:1 for text
(primary on every ground, text on a filled accent, drawer and appbar text),
3:1 for lines and drawer icons. A new preset that fails these does not ship.

Theme mode (**System / Light / Dark**) and accent are per device, held in
`localStorage` (`pspad.theme`, `pspad.accent`, `pspad.accent.custom`) via
`ThemePreference` — never on the `User` aggregate. A custom colour is kept
after switching back to a preset, so the edit swatch reopens on it.

**Application settings card.** Time zone, theme mode and accent share one
`SettingsPage` card titled "Application settings", not the account badge or
any menu (a control nested in a menu item is not reliably
keyboard-reachable). Time zone and theme mode are labelled outlined fields:
theme mode a `MudSelect`, not a button group. Accent is `AccentPicker`: a row
of round filled `MudIconButton` swatches, one per preset (a tick on the active
one, `aria-pressed` on each), then a last swatch with an edit icon that opens
`AccentColorDialog` — a static `MudColorPicker` (spectrum field, hue slider,
RGB/HSL/hex inputs via the mode switch, no alpha) with Cancel/Apply. The
dialog is `FullScreen` below the `md` breakpoint (`IViewport`), a small
centred dialog above it.

Time zone uses `MudAutocomplete` (type-to-filter over
`TimeZoneInfo.GetSystemTimeZones()`), not a plain `MudSelect` — a flat,
alphabetical list of 400+ IANA zone ids is unusable without narrowing by
typing.

**Typography scale.**

| Typo | Use |
|---|---|
| `Typo.h5` | page title, `Color.Primary` |
| `Typo.subtitle2` | card/section header (list name, goal name) |
| `Typo.body1` / `Typo.body2` | primary row/card content |
| `Typo.caption` | metadata (due date, counts, timestamps) |

**Color usage.** `Color.Primary` for the interactive/brand accent,
`Color.Error` for overdue and destructive affordances, `Color.Default` for
neutral icons. Never an inline hex color in markup — go through a `Color`
enum value or a palette-driven CSS variable.

**Icons.** Material icons via `Icons.Material.Filled.*` / `Icons.Material.Outlined.*`.
Filled = active/set state, Outlined = inactive/unset state — e.g. a starred
task shows `Filled.Star`, an unstarred one shows `Outlined.StarBorder`.

**One brand mark, boot to first screen.** `wwwroot/index.html` inlines the
`brand/icon.svg` mark (sage tile, animated check-path draw) as the boot
splash, painted before `MudBlazor.min.css` and before WASM starts. The same
mark renders inside the app via `Components/BrandLoader.razor` wherever the
shell would otherwise show a bare spinner, and on the four `Authentication.razor`
fragments (`LoggingIn`, `CompletingLoggingIn`, `LogOut`, `LogOutSucceeded`) —
never the library's default unstyled text. A `data-theme` attribute is
stamped on `<html>` from `localStorage["pspad.theme"]` before first paint
(falling back to `prefers-color-scheme`) so the boot splash never flashes
the wrong theme.

**Charts.** `MudChart` (bundled with MudBlazor, no extra dependency) takes
the active accent palette for free — the statistics screen's charts (daily
completions, tasks opened, outstanding-open, Inbox captures per week) are
the example. The Consistency heatmap is not a `MudChart` — see §1.

---

## 5. Navigation & auth screen shapes

**Sidebar**, top to bottom, one navigation tree at every width: a
non-interactive `AccountBadge` (avatar, display name, email — a label, not
a control), then a nav group of **My Day / Inbox / Goals / Statistics**,
divider, the user's areas in `Position` order plus **+ New area**, divider,
**Settings** / **App info**, then a spacer, then a footer (connection
status, current date/time, "PSPad · GPL v3"). There is no search field in
the sidebar (temporarily unreachable from the UI, tracked as a known gap,
not a page to recreate speculatively) and no dropdown on the account badge.

**Routes:**

| Route | Screen |
|---|---|
| `/` | My Day |
| `/inbox` | Inbox |
| `/areas/{areaId}` | area screen — list cards |
| `/lists/{listId}` | list screen |
| `/goals` | Goals |
| `/goals/{goalId}` | goal screen — every task of one goal |
| `/statistics` | Statistics — tiles, charts, Consistency heatmap and Inbox-captures bar chart, collapsed record feed |
| `/history` | redirects to `/statistics`, for bookmarks predating the rename (`adr/0038`) |
| `/settings` | Settings (account + sign-out; application settings: time zone, theme, accent; sync status; delete account) |
| `/app-info` | version, license, docs/repo links |
| `/search` | search results (currently unreachable from the UI) |
| `/welcome` | public, signed-out landing screen |
| `/authentication/{action}` | OIDC login/logout flow, branded fragments |
| `?task={taskId}` | task detail overlay, on any of the above |
| `?area={areaId}`, `?area=new` | area detail overlay, on any of the above |
| `?goal={goalId}`, `?goal=new` | goal detail overlay, on any of the above |
| `?inbox={itemId}`, `?inbox=new` | inbox item overlay, on any of the above |

**Signed-out visitors land on `/welcome`**, not a bare login redirect.
Sign-out ends the Keycloak session directly rather than only clearing local
state. The boot splash is held — no application chrome renders — until the
client has decided whether it is opening with a local session or sending
the user to sign in; see `specs/backend-spec.md` §6 for the decision
itself.

**Never build a second settings surface.** Account-level config (time zone,
theme, sign-out, account deletion) belongs on `/settings`. Per-item actions
belong on the item (`⋯` menu, or the page's own FAB per §3). There is no
third pattern.

**Sign-out lives inside the Account card**, under the avatar/name/email
block — not a standalone button elsewhere on the page.

**Account deletion is a "Danger zone" card**, last on the page, `Color.Error`
styling. Its button opens a `MudDialog` that asks the person to type their
own email address before the delete button enables (type-to-confirm, exact
match on `State.Email` trimmed and case-insensitive — there is no password
to check; see `specs/backend-spec.md` §6). The dialog awaits the delete call
inline (spinner, no redirect) and requires connectivity — same online check
as sign-out. A `keycloakRemoved: false` response still counts as success —
the dialog says the account's data is gone and sign-in removal needs an
administrator — and either way the client clears everything local: the
replica, the outbox too (unlike sign-out, which keeps it), and the session
store/tokens — composing the three existing `IReplica`/`IOutbox`/
`ILocalSessionStore.ClearAsync()` calls rather than adding new storage
interop — then lands on `/welcome`, same destination as sign-out.
