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

### Whole day, scrolled only when it does not fit

`DayTimeline` always draws 00:00–24:00. The hour height is the CSS custom
property `--pspad-hour` on `.pspad-timeline`:
`clamp(32px, (100dvh - offset) / 24, 56px)`, and the timeline's
`max-height` is `calc(100dvh - offset)` with `overflow-y: auto`. When the
viewport has room for 24 hours at 32px or more, the whole day shows with no
scroll; otherwise the timeline scrolls inside itself. `offset` is the space
above and below the timeline: 240px from `md`, 360px below `md` (top bar, week
strip, tabs, bottom navigation).

Every vertical position is a CSS expression in hour units,
`calc(var(--pspad-hour) * minutes / 60 + growth)`, so the component never
needs the measured height. `growth` is the sum of the extra pixels of grouped
clusters that end at or before the position (see below).

A hidden anchor (`pspad-timeline-anchor`) marks where the view starts: an hour
before now on today, else 30 minutes before the first block, else 08:00, never
before 00:00. `wwwroot/js/timeline.js` scrolls the timeline to the anchor the
first time the timeline has a size (a `ResizeObserver`, since on a phone it
is hidden behind the Tasks tab) and again when the day changes.

A day without timed tasks still draws the axis (and the now line on today);
the muted "Schedule empty for today." line sits above it.

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
