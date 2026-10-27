# My Day schedule and sections — design

Issue #185. Proposal with screenshots: artifact "My Day fixes #185".

## Problems

1. The schedule axis runs from the first start to the last end, so it changes
   every day and an empty day has none.
2. Three tasks at the same hour get three lanes of ~100px in the 380px column;
   the name disappears.
3. Lanes are laid out from real times while a block has a minimum height, so
   13:00–13:10 and 13:15 share a lane and their blocks draw over each other.
4. On very wide screens the ledger leaves a gap of ~1000px between name and
   details.
5. Timed tasks appear only on the axis; the phone's Tasks tab never lists them.
6. Completed sections are 24px narrower on each side than the groups above
   them: MudBlazor's `.mud-expand-panel .mud-expand-panel-content.mud-expand-panel-gutters`
   (0-3-0) beats `.pspad-completed .mud-expand-panel-content` (0-2-0).

## Decisions

### Fitted to the screen, busy hours larger, empty hours folded

Superseded the earlier "whole day, scrolled when it does not fit" (#202
follow-up, mockup "My Day Schedule Fit"). The timeline never scrolls.

- `.pspad-timeline` is `height: calc(100dvh - offset)` (at least 320px) with
  `overflow: hidden`; `offset` is 240px from `md`, 360px below it (top bar,
  week strip, tabs, bottom navigation).
- `State/TimelineAxis` splits the day into segments. An hour with no task and
  not the current hour is empty; three or more empty hours in a row
  (`FoldFrom`) fold into one band (`pspad-timeline-fold`, 28px, labelled
  "00:00–07:00", a button that unfolds it until the day changes). Other hours
  weigh 1 when empty, 2 with one or two tasks or as the current hour, 3 with
  three or more.
- Every position is a share of the timeline's own length:
  `calc((100% - folds × 28px) × weightBefore / totalWeight + foldPixelsBefore)`,
  set as `--at` and `--size` on each hour, band, block, group and the now
  line. The same values serve `top`/`height` and `left`/`width`, so the
  component never measures anything and CSS picks the orientation.
- A block is at least 24px; a group at least its lines (`--needs`). The
  earlier "growth" that pushed the axis below a group is gone: a busy hour
  is already larger.
- From 1600px My Day stacks the schedule above the tasks
  (`grid-template-areas: "schedule" "lists"`) and the timeline runs left to
  right, 176px tall, lanes stacked top to bottom, bands with vertical text.
- Each shown hour holds two half-hour buttons (`pspad-timeline-slot`,
  `data-time`, "Add a task at 09:30") when the page passes `OnAdd`. My Day
  opens `?task=new&due={day}&time=HH:mm` (`TaskQuery.ForNewTask(…, time)`);
  the task panel starts the draft at that time for an hour. Blocks sit above
  the slots, so a click on a task still opens it.
- An empty day is one band, 00:00–24:00; on today the current hour stays
  open between two bands. The muted "Schedule empty for today." line sits
  above it.

### Visual span, lanes and groups

- A block is at least 24px tall. Lanes and clusters are computed from the
  **visual span**: the real span, but at least 45 minutes (24px at the 32px
  minimum hour), so blocks that would touch on screen never share a lane.
  A task without an end still takes 30 minutes; an overnight one ends at 24:00.
- A cluster needing one or two lanes keeps side-by-side blocks.
- Within a cluster the longer task takes the first lane.
- A cluster needing three or more lanes whose tasks all start within half an
  hour of the first becomes one full-width group block
  (`pspad-timeline-group`) spanning the cluster, holding one 24px line per
  task (`pspad-timeline-group-line`: checkbox, time, name; the name opens the
  task), ordered by start then name.
- Any other cluster needing three or more lanes (a long task with others
  inside it) keeps its first-lane tasks as blocks in the left half; the rest
  are clustered again among themselves in the right half, a lone task as a
  block, two or more as a group at their own time, its lines showing only
  their start (`pspad-timeline-task-narrow`). Nothing is listed away from its
  time. The block is
  `max(lines × 24px + 4px, its span − 2px)` tall; what it needs beyond its
  span is the group's growth, added to every position from the group's end
  on, so the axis below moves down rather than being covered; a block that
  runs past a group grows by the same amount.
- A block shorter than 90 minutes of real time is one line (time and name,
  `pspad-timeline-task-short`); from 90 minutes it shows time over name, and
  from two hours also the list name, so nothing is cut at the 32px hour. A
  one-line block sharing the width (`pspad-timeline-task-narrow`) shows only
  its start; the block's `title` carries the name and full time.

### Scheduled section

The task sections gain **Scheduled · N** (`pspad-day-scheduled`,
`Color.Primary` heading) between Starred and "{Day}, any time", listing
`DayPlan.Scheduled` (pending timed tasks, by start) as `TaskRow`s, which show
the time in their meta line. It shows on every width: the timeline says when,
the list gives quick ticking and starring. Absent when empty.

### Wide screens

From 1600px the My Day row groups (`pspad-row-ledger`) flow into two columns
with the meta line under the name, the same look as `pspad-row-group-cols`;
the schedule column grows from 380px to 420px. Between 960 and 1600px the
ledger stays.

### Completed

`MudExpansionPanels` under `pspad-completed` (My Day, list, goal) set
`Gutters="false"`, so the completed group lines up with the groups above it.
No new CSS.

## Out of scope

Drag to reschedule, a week view, changing `TodayRule`.
