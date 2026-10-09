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
| A section that opens on click (the record feed, a list's Completed) | `MudExpansionPanels` + `MudExpansionPanel` | a hand-rolled show/hide flag |
| A section heading (My Day, Settings, Statistics, Goals, Goal, panel subsections) | `Components/SectionHeading.razor` (`h2`, `pspad-section-heading`; `Title`, `Count`, `Hint`, `Color`) | a `MudText Typo.subtitle2` or an overline |
| A card's collapse control | the header's icon tile in `ThingCard`, a `MudIconButton` with `aria-expanded` | a separate chevron or `ExpandMore` button |
| A task checkbox | `MudCheckBox` with `UncheckedIcon` `RadioButtonUnchecked` and `CheckedIcon` `CheckCircle` (round) | the square default |

Before adding a new `pspad-*` class, check this table first. If nothing
fits, the class is legitimate — but it means the styling is specific to
PSPad's brand or a one-off page need, not a generic layout or component
problem MudBlazor already solves.

A Mud component's `UserAttributes` dictionary cannot be combined with
another unmatched attribute (`aria-label`, say) on the same element in
MudBlazor 9 — the loose attribute wins and `UserAttributes` is dropped, so
an accessible label goes into the dictionary alongside everything else
rather than as a bare attribute beside it.

Not every `pspad-*` class carries a CSS rule. Several exist purely as a
stable selector for tests to find a MudBlazor element that has no other
reliable hook (`pspad-sign-out`, `pspad-change-password`, `pspad-account-card`, `pspad-delete-account`,
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
`MudGrid Spacing="4"`, one `MudItem xs="12" sm="6" lg="4" xl="3"` per item:

```razor
<MudGrid Spacing="4">
    @foreach (var item in items)
    {
        <MudItem xs="12" sm="6" lg="4" xl="3">
            <Card ... />
        </MudItem>
    }
</MudGrid>
```

One column on a phone, two from `sm` (600px) — still two at `md` (960px),
where the permanent sidebar leaves too little width for three — three from
`lg` (1280px), four from `xl` (1920px). Row-flow, not
column-flow — DOM order stays reading order for keyboard and screen-reader
navigation.

**The area board is masonry, not a row grid.** List cards differ wildly in
height, so a row grid leaves an empty band under every short card.
`AreaBoard` renders its cards through `Components/MasonryGrid<TItem>`: the
same breakpoints decide a column count (`MasonryGrid.ColumnsFor`: 1 below
`sm`, 2 at `sm`/`md`, 3 at `lg`, 4 from `xl`, read from `IBreakpoints`), and
item *i* goes to column *i mod n* (`MasonryGrid.Columns`). Markup stays
MudBlazor: `MudGrid Spacing="4"` → one `MudItem xs="12" sm="6" lg="4"
xl="3"` per column → `MudStack Spacing="4"` of cards, no custom CSS. The
first row always holds the top *n* lists of the user's order, and a phone
shows the order top to bottom. This is the one exception to row-flow: DOM
order is column by column (`adr/0051`). The empty state stays a single
card in a plain `MudGrid`.

Applied on: `GoalsPage` (goal cards, active and
achieved separately), `Today` (overdue, any time, starred, coming up
and completed each as their own grid; the schedule is a single column), `InboxPage`, `ListPage` (open and completed separately),
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
inset, so the FAB never covers the last row. Below `md` that padding is
`calc(96px + 72px)`, the extra 72px being `BottomNav`'s own height, so the
bar never covers the last row either. Below `md`, every scrolling element
(`*`) hides its scrollbar
(`scrollbar-width: none`, `::-webkit-scrollbar { display: none }`) while
still scrolling.

**Top spacing.** The container's top margin is 16px below `md`; from `md`
up it has no top margin and a 24px top padding instead (`mt-4 mt-md-0
pt-md-6`). `MudMainContent` reserves top padding for the phone app bar;
from `md` up, where that bar is hidden, `app.css` removes it
(`.mud-main-content { padding-top: 0 !important; display: flow-root }` in a
`min-width: 960px` query). Desktop page and sidebar both start at y=0: a
top margin on the page's first child must never escape the main area —
with no padding above it, it collapses through `.mud-main-content`,
`.mud-layout` and `body`, pushing the body down and leaving a strip of
canvas at the top of the window. The `flow-root` keeps such margins inside
the main area, and `.mud-drawer.mud-drawer-persistent.mud-drawer-pos-left
{ top: 0 }` pins the sidebar, which MudBlazor otherwise leaves at its
static position (it sets no `top` on a persistent left drawer).

**Sidebar breakpoint.** The sidebar is permanent at `md`+ (≥960px). Below
`md` there is no navigation drawer at all: navigation is `MobileTopBar`
plus `BottomNav` plus `AccountDrawer` (`adr/0050`,
`specs/mobile-navigation-design.md`), using MudBlazor's display utilities
(`d-none d-md-flex` / `d-md-none`), never `MudHidden` — `MudHidden` resolves
through `IBreakpointService`'s JS round trip and renders its default branch
before the first callback, flashing the wrong navigation on load. Both
branches live in the DOM at all times, separated only by CSS resolved
before first paint.

**Mobile app bar.** Below `md`, `Layout/MobileTopBar.razor` is a
`MudAppBar` (`pspad-top-bar`, not `Dense`) `--pspad-top-bar-height` (64px) tall on the surface colour with a
`--pspad-line` bottom border, 16px side padding (4px on the left when a back
button shows) and a 12px gap; `MudMainContent`'s top padding follows the
same token, and the pull-to-refresh indicator sits 10px under the bar. Left to right: a back `MudIconButton` (`ArrowBack`) when the
current `PageHeading` set a `BackHref`; the page icon tile when the page has
one (`PageIcon`, 36px, radius 10px); the page title (18px, weight 600,
`TextPrimary`, one line with an ellipsis) over an optional subtitle (12.5px,
`TextSecondary`, one line with an ellipsis — a list screen shows its area's
name and open-task count); `ConnectionStatus` (a `CloudOff` icon with a tooltip, nothing while healthy; offline is confirmed by `/health`, `adr/0058`); `SyncButton WithStamp` (a `Sync` icon button, spinning while a sync runs, red after a
failed one, disabled offline; its tooltip and `aria-label` carry the stamp text); a 36px
`MudAvatar` in a 44px button that opens `AccountDrawer`. Every icon button in
the bar is 44px. Title, subtitle and icon come from
`State/PageHeader.cs`, a scoped service `PageHeading` writes to on every
parameter set, so the phone and desktop titles cannot drift.

**Last updated.** `Components/PageHeading.razor` carries it on every screen with
a heading. `Layout/SyncStamp.razor` is a caption — "Updated 2 min ago",
"Updating…" while a sync runs, "Offline · updated 3 days ago" (warning colour), "Couldn't update · updated
5 min ago" (error colour) when the last attempt failed,
"Not synced yet" — whose `title` holds the exact local time; it re-reads every
30 seconds. From `md` up it sits right-aligned in the heading beside
`Layout/SyncButton.razor` (a small `Sync` `MudIconButton`, spinning while a
sync runs, disabled offline). Below `md` the heading draws nothing: the same text
lives in the tooltip of the top bar's `SyncButton` (see above), and
`SyncStampText` builds it for both. The time
comes from `ISyncStatus.LastSyncedAt` (`SyncCoordinator`), set only when a
sync reached the server and kept in the replica's `meta` store so a cold,
offline start still shows it; `ClearAsync` purges it with the rest.
Settings → Sync (`id="sync"`) shows a status line ("Syncing…", "Couldn't sync." in the error colour, "N pending" or "Everything is synced."), "Last synced …" (re-read every 30 seconds), the messages of the last rejected changes when there are any, and a labelled `SyncButton` ("Sync now"). Every `SyncButton` turns `Color.Error` after a failed sync.
Below `md`, `Layout/PullToRefresh.razor` (mounted once in `AppShell`,
`wwwroot/js/pullrefresh.js`) is the refresh path: dragging down 80px from the
top of the page, with no panel, dialog or drawer open, runs the same sync;
offline it does nothing. Both call `ISyncTrigger.SyncNowAsync`.

Below `md`, `Layout/BottomNav.razor` is a `MudAppBar Bottom="true"` with
five equal slots — Inbox, Areas, My Day, Goals, Statistics — My Day raised
as the centre `MudFab`, always filled `Color.Primary`; the other four are
`MudButton`s (icon over label, the sidebar's own icons) whose active slot
gets a primary icon, a bold label and a tonal pill. The active slot is a
pure function of the path (`State/NavTab.cs`); query strings never change
it.

Below `md`, the avatar in `MobileTopBar` opens `Layout/AccountDrawer.razor`
— a `MudDrawer`, `Anchor.End`, `DrawerVariant.Temporary`, 300px — holding
`AccountBadge`, Settings and App info as `MudNavLink`s, then
`Components/SidebarFooter.razor` (connection status, clock, "PSPad · GPL
v3"), the same footer component the permanent sidebar uses. Navigating
closes the drawer.

**Status belts.** The app's own state — a new version ready, the server
unreachable, changes the server rejected — shows as a belt, never a toast.
`State/StatusBelts.cs` (scoped) holds at most one belt per
`BeltKind { Update, Offline, Rejected }`, newest first; `Show` replaces a
kind's belt and moves it to the top, `Dismiss` hides it until `Clear` says
that state ended, after which the next `Show` appears again.
`Layout/StatusBeltStack.razor` renders them in `MudMainContent` before the
page container, in flow, so they push the page down and never cover it: a
`MudAlert` (`Variant.Text`, `Dense`, `role="status"`, the severity's icon)
holding the text (13.5px), an optional text action (13px, 600) and a
`Dismiss` ✕ icon button. Ground is the severity colour mixed 14% into the
page background, text is `TextPrimary`, icon and action are the severity
mixed 60% into `TextPrimary`, the ✕ is `TextSecondary` (`PSPadThemeTests`
checks text and action ≥ 4.5:1 and the ✕ ≥ 3:1, light and dark; status
colours, text and background do not follow the accent). Desktop (md+): 24px inset like the content, 8px
between belts, radius 10px, 12px above the page heading. Phone: directly
under `MobileTopBar`, edge to edge, no radius, a `--pspad-line` bottom
border.

| Kind | Severity | Text | Action | Ends |
|------|----------|------|--------|------|
| Update | Info | "A new version of PSPad is ready." | Reload — activates the waiting service worker and reloads (`adr/0040`) | never; ✕ leaves the old version running until every tab closes |
| Offline | Warning | "Can’t reach the server — working from local data. Changes sync when you’re back." | Retry — `SyncNowAsync` | `ServerReachability` reachable again clears it |
| Rejected | Error | "1 change couldn’t be saved." / "N changes couldn’t be saved." | Details — `/settings#sync` | never by itself; every newly rejected command (by command id) reopens it, even after a dismiss and even with the same reason; a domain rejection still queued and rejected again on the next sync stays dismissed |

`AppShell` raises Update and Offline; `SyncCoordinator` raises Rejected and
keeps the messages of the last sync that had rejections in `ISyncStatus.LastRejections` (a clean sync keeps them), which the
Settings Sync card (`id="sync"`) lists under "Couldn’t be saved".
`PublicLayout` shows neither state, so it has no stack.

**Small messages.** One action's outcome ("Couldn't star the item.",
"Copied", "Snapshot published. Link copied.") stays an `ISnackbar` message,
configured once in `Program.cs` through `Theme/SmallMessages.cs` so both
layouts share it: one at a time (`MaxDisplayedSnackbars = 1`,
`NewestOnTop`, `PreventDuplicates`), 4 s, a close icon, 150 ms in and out,
`Variant.Text` on an opaque `--pspad-message-ground` (white light, `#2C2C2C`
dark), `TextPrimary` 13px, radius 10px, the icon in the severity colour.
`BottomCenter`: on phones 16px above the 72px bottom nav, on desktop moved
to the bottom left of the content (past the open sidebar). App state never
goes through a snackbar.

---

## 3. Page structure

**Every page gated on `_loaded`.** A page carries `bool _loaded`, set true
only at the end of its data reload. Until then it renders a skeleton
(`RowSkeleton` or `CardSkeleton`), never an empty state or a "Nothing here"
message — a screen must never show an empty state it has not verified.

**Title pattern.** A page's title is `<PageHeading Title="…" />`
(`Components/PageHeading.razor`), with `BackHref`, `BackLabel`, `BackClass`,
`Subtitle` and `Icon` as needed, rendered both in the loading and
loaded branches so nothing jumps on load. Desktop row, `align-items: center`:
[back] [icon tile] [title over subtitle] … sync stamp and Sync button, on
the page background (no band), 14px gaps. The desktop row lines up with
the sidebar's `AccountBadge`: the badge's avatar row starts 28px from the
top (the nav's 12px padding plus the badge's 16px) and is 40px high, so
its centre is at 48px. The heading row starts 24px into the page (the
container's `pt-md-6`), is 48px high (`.pspad-page-heading { min-height:
48px }`) with its items centred — the 40px tile spans the avatar's 28–68px
and, with a subtitle, the title's centre meets the account name's (~38px).
The page content starts 20px under the row (`mb-5`). The
title is the page's one `h1` (`MudText Typo.h5 HtmlTag="h1"`, 20px,
line-height 1.15, `TextPrimary`, weight 600, `pspad-page-title`); the back `MudIconButton`
shows when `BackHref` is set — a screen nested under another, such as a list
under its area, links back to the parent screen. `Icon` (a MudBlazor icon
path) renders in `Components/PageIcon.razor`: a `pspad-page-icon` tile, 40px,
radius 12px, `--pspad-tint`, primary icon, `aria-hidden`. The title and
`Subtitle` (`pspad-page-subtitle`, 12.5px, `TextSecondary`, 2px under the
title) stack in one column (`pspad-page-heading-text`). It sits inside
`d-none d-md-flex` on desktop and feeds the phone top bar (`MobileTopBar`,
via `PageHeader`) the same title, subtitle, icon and back link below `md`.
The Area board passes `Icons.Material.Outlined.Folder` (Shared with me:
`People`) and an `AreaSummary` subtitle (below); the list screen passes its
`ListIcon.For(list)` with `IconLabel` `ListIcon.LabelFor(list)` (a
visually hidden `pspad-sr-only` span in the `h1`, since the tile is
`aria-hidden`) and "{area} · {m} open task(s)" (a reference list: the
area alone); the goal screen passes `Flag`; other screens have no icon.
Counts are singular for one, plural otherwise, and zero open tasks reads
"no open tasks" (`OpenTaskCount`): "1 list · no open tasks", "1 list · 1
open task", "2 lists · 2 open tasks". Open tasks are the ones a card counts:
not deleted, not completed, not ended. One component means the two cannot
drift. The area subtitle (`AreaSummary`) depends on the list kinds: task
lists only, "{n} list(s) · {m} open task(s)" ("· no open tasks" at zero);
reference lists only, "{n} reference list(s) · {k} item(s)" ("· no items"
at zero); both, "{n} lists · {m} open task(s) · {k} item(s)" with n every
list, a zero part left out, except "no open tasks" stays when there are no
items either ("2 lists · 4 items", "2 lists · no open tasks"). Items are the
reference lists' items that are not deleted. A page's loading branch renders `<PageHeading Title="" />`
so the top bar never keeps the previous screen's title.

**My Day** shows one day. `PageHeading Title="My Day"` (title … sync stamp
and sync button, nothing else), then a `WeekStrip` (`Components/WeekStrip.razor`;
`Day`, `Today`, `DayChanged`, `Busy`) showing the Monday–Sunday week that
holds the day: a small ‹ (`pspad-week-prev`, "Previous week") and › (`pspad-week-next`,
"Next week") moving the day by ±7 days, and seven `MudButton`s
(`pspad-week-day`, `aria-label` "Thu, 8 Oct"), each a column of the weekday
initial, a circle with the day number (34px; 32px below `md`, so seven
days and both arrows fit a 360px phone) and a 5px dot when the day is in
`Busy`. Today's button carries `pspad-week-day-today` and
`aria-current="date"`, its circle a primary ring; the picked day carries
`pspad-week-day-picked`, its circle filled primary with contrast text. A
swipe on the days turns the week: `WeekStrip.SwipeDirection(dx, dy)` gives
next (swipe left) or previous (swipe right) when the horizontal travel is
at least 48px and more than the vertical. A **Today** button
(`pspad-week-today`, outlined, primary) shows only when the day is not today,
on every width, and a calendar icon button (`pspad-week-pick`, "Pick a
date") opens a `MudDialog` (the `DueDateRow` pattern) holding a static
`MudDatePicker`, whose actions are **Today** and **Cancel**; picking a day
closes it.

On desktop the strip is an outlined `MudPaper` across the content width
under the heading: the arrows and days, then at its right
`DueDateRow.Describe` of the day ("Today · Thu, 8 Oct" for Today, Tomorrow
and Yesterday, else just "Fri, 16 Oct") over a muted "Week 41 · October
2026" (ISO week), then Today and the calendar button. Between 960 and
1100px the label block is hidden so the strip never overflows; the picked
circle still names the day. Everything the strip renders (labels, month,
aria-labels, weekday initials) is formatted in the invariant culture; only
the words Today, Tomorrow and Yesterday come from `DueDateRow.Describe`.
Below `md` it is a bar directly under `MobileTopBar`, full width with no
side margins and a bottom border only: a header row with the picked day's
"October 2026" and the Today/calendar buttons, the days below it.

`Busy` is computed on the page from the already loaded tasks: each day of
the visible week whose `TodayRule.Plan` has a Scheduled or any-time entry,
or Overdue when the day is today. Past days are never busy, since their
plan holds only Completed.

The day lives in the URL as `/?day=yyyy-MM-dd`; `/` is today and a
malformed `day` falls back to today. Arrows, days, swipes and the picker navigate with
`replace: false`, so Back steps through visited days. `TaskQuery.For` and
`Without` keep `day` (opening a task gives `?day=…&task=…`, closing the
panel keeps the day); `ForNewTask` does not.

Sections, top to bottom, each hidden when empty:

1. **Overdue** (`Color.Error` heading) — today only.
2. **Schedule** (`Color.Primary` heading) — tasks with a time, one column,
   max 760px: a time gutter, then the `TaskRow` card. Desktop gutter 132px,
   `09:30–11:00` on one line (`nowrap`); below `md` 52px, start over end.
   An overnight time (end before start) adds a muted `(+1)`
   (`pspad-schedule-nextday`) after the end on desktop, under it in a smaller
   size below `md`; the task stays on its start day, ordered by start. Overlapping
   tasks simply follow each other. `TaskRow` there hides its own time.
3. **{Day}, any time** — "Today, any time", "Tomorrow, any time", "Fri, 9
   Oct, any time" (`DueDateRow.Describe`): the untimed tasks in the card
   grid. When Schedule and this section are both empty (and Overdue on
   today) it stays with "Nothing planned for today." / "…for Fri, 9 Oct."
4. **Starred** — `SectionHeading` with `Hint` "· when you have time"; card grid.
5. **Coming up** — today only; card grid grouped under a muted caption per
   day (`DueDateRow.Describe`), shown open.
6. **Completed (N)** — a collapsed `MudExpansionPanel` on today and future
   days. On a past day it is the only section, a plain open section with
   "Nothing completed on Wed, 7 Oct." when empty.

Each task is its own outlined `MudPaper` card in a `MudItem`, sized to its
content, never rows inside one shared paper. Membership comes from
`TodayRule.Plan`, never from the page. Coming up's reach is per task: its
lead time, one week by default (`adr/0053`). The goals summary is not on
the page (`GoalSummaryCard` stays for the goal screens).

Ticking: a recurring row ticks the occurrence on the day shown (a Coming up
row, its own day). A one-time row completes now, so a future task ticked
from its day lands in today's Completed. The page re-plans on
`SyncRevision` and on day change; the sidebar's My Day count stays today's
`Select`.

**Goal screen.** `/goals/{goalId}` mirrors the list screen: back arrow to
`/goals`, the goal's name as title with a small outlined status `MudChip`
(In progress / Achieved — `Color.Success` — / Not achieved) and its due
date as a caption under it. Tasks come first: every open task as its own
card (list name on each row), then a collapsed **Completed (N)**. The charts are a separate section at the bottom (`mt-8`, their own
`MudGrid`): the **Progress** card and, for dated goals, the **Pace** card,
each half width from `md` up. Tasks and charts never share a grid. With
no open tasks it shows an `EmptyState` "No open tasks." with no create
action — a task is created in a list, not a goal.
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

**Goal pace card.** `GoalPaceCard`, in a `MudItem` (`xs="12" md="6"`)
beside the progress chart, shown only when the goal has a due date
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
`GoalCard` are thin wrappers over one `ThingCard` — icon tile, title
link, open count, `⋯` menu, at most five open `TaskRow`s and a
**Show all N** link to the thing's own screen — and differ only in their
header extras and row caption: a goal card's rows name their list. The
header's 30px icon tile (radius 9px, `--pspad-tint`, primary icon) is the
collapse toggle: a button with `aria-expanded` and `aria-label` "Collapse
{name}" / "Expand {name}" (no chevron). The name stays the link to the
list, `TextPrimary`, 14.5px, medium. The count is a muted pill
(`--pspad-hover`, `TextSecondary`, 11px) showing the number alone, followed
by a visually hidden " open" (`pspad-sr-only`), so it reads "N open". The `+` and `⋯` actions are 28px, radius 8px,
`TextSecondary`. There is no divider under the header or above **Show all
N**, a primary text link at 12.5px. The card's body sits in a
`MudCollapse`, so collapsing and expanding animate its height and the
cards below slide with it. A task card with no open tasks and no
`EmptyText` shows a muted row (`pspad-card-empty`): "No open tasks · " and
an **Add one** text button (`pspad-card-empty-add`, `aria-label` "Add a task
to {name}") raising `OnAdd`; the " · " is its own `aria-hidden` span drawn
only with `OnAdd` —
`ListCard` passes its header `+` callback, `OnAddTaskClick`. With no `OnAdd`
the row reads "No open tasks" alone; a card with `EmptyText` (a goal card,
an empty reference list) shows that text instead. A single component per
concept means a rule like never-overdue-for-recurring-tasks cannot drift
between the screens that display it.

**One row shape.** `TaskRow`, `ReferenceRow` and `InboxItemCard` share it,
so every row and card is the same height wherever it appears — My Day, the
list and goal screens, the Inbox and area-board cards (`pspad-row`, 60px).
Rows carry no dividers: spacing separates them, and a row inside a card has
an 8px radius and `--pspad-hover` on hover. The name is `TextPrimary`, 14px;
the second line 11.5–12px `TextSecondary` (the time keeps primary and medium
weight, an overdue date keeps the error colour); the star is
`TextSecondary` unstarred, primary filled when starred. `ReferenceRow` gets
the same row, star and meta treatment.
- A 40px leading slot: the task's round checkbox (`RadioButtonUnchecked` in
  `ActionDefault`, `CheckCircle` in primary when checked), or a muted icon
  (bookmark for a reference item, inbox tray for an Inbox item) so names
  line up.
- The name on one line, cut with an ellipsis, the full name as its `title`.
- A second line only when there is something to show; without one the name
  centres vertically in the same fixed height. It never wraps: what does
  not fit is clipped.
- Only the star on the right (none on an Inbox card).

A task's second line runs, in this order: the time (`TimeRow.Describe`:
`09:30–11:00`, `09:30` without an end, `22:00–01:00 (+1)` overnight; `Color.Primary`, medium weight; hidden in My Day's
Schedule, whose gutter shows it), due date (red when overdue,
"Until 12 Oct" on a repeat), step progress (checklist icon and `0/4`), the
repeat icon, the description icon, the priority dot, then the list name
where the screen passes one (My Day, goal screen and goal cards). Each
piece keeps whole; only the list name shrinks, with an ellipsis. The day
a task was added is never on a row — the task panel's footer carries it.

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
only right-anchored detail drawer: 420px from `md` up, full width below it.
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
   Given `Draft` (a `List<string>`) instead of `Task`, it edits that list
   in place and sends nothing — rows show a muted empty-box icon rather
   than a checkbox. The Inbox convert form uses it this way.
4. Property rows, each a `PropertyRow` — icon · label · value, the whole
   row a `MudMenu` activator; the label sits in a fixed 88px column, never
   wraps, and a long value wraps beneath itself rather than running into
   the label; an empty value reads in the muted text colour
   ("No due date", "No goal"), a clearable one carries a trailing
   ✕:
   - **Kind** (`MudToggleGroup<bool>`, `pspad-task-kind`, above the rows) —
     One-time | Repeating; its value is whether the task (or Add's draft)
     has a rule. One-time shows Due; Repeating shows Repeat then Until.
     Switching to Repeating sets Daily from today and clears the due date;
     switching to One-time clears the repeat and Until (`adr/0053`).
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
     Yearly on today's date, Custom… Presets start today. Custom… opens a `MudDialog`:
     Every [1–99] [days | weeks | months | years], weekday chips (weeks, Monday
     first), On day 1–31 (months), Starts (date picker, default the current
     rule's start or today; years show no weekday chips or day field); moving Starts moves the weekday chip or
     day-of-month default with it until the user has picked one; Save
     stays disabled until the fields are valid. Setting a repeat on a task
     whose due date falls before the rule's start clears that date (sent
     after `SetTaskRecurrence`), so the repeat does not start already
     ended. The value reads "Daily", "Weekly on Mon, Thu", "Every 3 weeks
     on Mon", "Every 2 days", "Every 6 months on day 15", "Yearly on 15 Mar",
     "Every 2 years on 15 Mar". A repeating task
     shows its last seven occurrences as chips under the row, then a muted
     tally caption: "Not done yet", "Done N times", or "Done N times · M in
     a row" (`RepeatTally`).
   - **Time** (`TimeRow`, `pspad-task-time`, under Due/Until; shown when
     Due/Until is set or the task repeats, like Remind me) — the clock icon
     and "Time" label on the left (`pspad-time-head`), and on the right two
     stacked rows (`pspad-time-line`): **From** and **To** labels (12.5px,
     `TextSecondary`, 40px wide) each followed by a 24-hour outlined, dense,
     typeable `MudTimePicker` filling the rest of the row, `aria-label`
     Start / End (`pspad-time-start`, `pspad-time-end`), ending in a
     `Schedule` adornment icon that opens the picker (MudBlazor 9.9 has no
     open-on-click for an editable picker, so the icon is the click target
     and the field stays for typing); End is disabled until Start has a
     value. A ✕ (`pspad-property-clear`, `Size.Small`, the shared
     `PropertyRow` clear) on the From row clears both and lines up with
     Due's ✕; with no time an empty placeholder of its width keeps the
     column, and the To row keeps the same right inset. Below `sm` (600px)
     the head sits above the From/To rows so the pickers get the full width. End equal to Start shows
     "End must differ from start" under the row and sends nothing; an End
     before Start is sent and shows a muted "Ends the next day (+1)" under
     the row. Edit
     mode sends `SetTaskTime` on each valid change; Add keeps it in
     `TaskDraft.Time`, sent after `SetTaskDueDate`.
   - **Remind me** (`LeadTimeRow`, `pspad-task-lead`, after Due/Until;
     on a one-time task only once it has a due date, since it counts back
     from one) —
     1 day, 3 days, 1 week, 2 weeks, 1 month, Custom… (a `MudDialog`: [1–99]
     [days | weeks | months]). Empty reads "1 week before" in the muted
     colour; a set value ("3 days before") carries a ✕. In Add the draft's
     lead time is sent as `SetTaskLeadTime` after the due date. The menu and
     the Custom dialog both open with a muted caption (`pspad-lead-hint`)
     reading "How early the task shows in Coming up on My Day. On its due date
     it moves to Today as usual."
   - **Priority** — the four fixed levels with coloured dots.
   - **Goal** — the user's goals.
   - **List** (outside Add) — lists grouped under area headings; picking
     one moves the task at once. There is no Move button.
5. **Description** (outside Add) — a `PanelSection` titled "Description"
   holding the task's `MarkdownField` (see below).
6. Footer — "Created …" on the left, a red trash `MudIconButton` on the
   right that asks via `ConfirmDialog` before deleting. In Add the footer
   holds only **Add task**, and Enter in the name field adds too.

**An existing area's panel orders its lists.** Under the Name field,
`AreaDetailPanel` shows a `PanelSection` **Lists · N** (`pspad-area-lists`)
with one row per list in board order (`ListOrder.InArea`): `ListIcon`, the
name, then a `DragIndicator` handle (`pspad-drag-handle`) as the last
element — the `ReferenceFieldList` row pattern. Rows reorder by drag only,
through `MudDropContainer`/`MudDropZone AllowReorder`: a drop sends
`ReorderLists` with the displayed order and the zone index, and saves at
once, like every edit of an existing thing; drops while one is in flight
are ignored. `MudDropZone` keeps its own per-item index, so the container
is re-keyed after each reload to render the new order. Drag relies on the
browser's HTML5 drag events — there is no button fallback. No lists → a muted
"No lists yet.". A new area has no Lists section. Every other list picker
(task, Inbox and reference-item panels) groups lists by area in the same
order through `ListOrder.Arrange`.

**Creating and editing always happen in a side panel, never a popup.**
Every thing — area, list, task, reference item, goal, Inbox item — is
created and edited in its `DetailPanel`, addressed by query string. Every
panel opens the same way: the small `Typo.body2` title, then the unboxed
name field (`Typo.h6`, no underline, `Placeholder="{Thing} name"`, autofocus
and Enter-adds in add mode) directly under the header, then everything else
— kind, area, status, due date, labels, lists — below it. Editing an
existing thing saves each change; adding closes the panel and never opens
the new thing (a new area is the one exception: it lands on its own board,
where its first list gets added). A card's `⋯` menu offers **Edit** (opens
the panel) and **Delete**, via `ThingMenu`. Popups are only for confirming
a delete (`ConfirmDialog`, `DeleteAccountDialog`) and for pickers opened
from inside a panel (due date, custom repeat, custom accent).

**Area and goal detail use the same shell.** `Layout/AreaDetailPanel.razor`
is addressed as `?area=new` (sidebar **+ New area**) or `?area={areaId}`
(the area's **Edit area** FAB item), via `AreaQuery`.
`Layout/GoalDetailPanel.razor` is addressed as `?goal=new` (the Goals FAB,
or the empty state) or `?goal={goalId}` (a goal card's **Edit**, or the goal screen's **Edit goal**), via `GoalQuery`. Both open with the unboxed name
field ("Area name" / "Goal name") under the header. A goal adds a **Status** `MudSelect` (In progress /
Achieved / Not achieved, existing goals only) and a **Due** `DueDateRow`
(in add mode too). The Goals page shows in-progress goals as cards ordered
by due date, undated last. A card shows "Due …" on its own line under the
name, in `Color.Error` once the date has passed. A card's `⋯` menu offers
the two closing statuses.
- **Sections:** Achieved and Not achieved goals get their own always-visible
  sections below. Each section heading is a `SectionHeading` with a count
  pill and no icons. An "In progress" heading
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

**Lists use the same shell.** `Layout/ListDetailPanel.razor` is addressed
as `?list=new&inarea={areaId}` (`ListQuery.ForNewList`) from the area's
**New list** FAB item or its empty state, and as `?list={listId}`
(`ListQuery.For`) from a list card's **Edit** or the list screen's **Edit
list** FAB item — full screen below `md`, a side panel from `md` up.
`ListQuery.From` ignores URLs carrying `task=` or `item=`, whose `list=`
names a target list rather than opening one. Under a `New list · Area` /
`List · Area` title sits the unboxed "List name" field, then:
- New: a `MudToggleGroup<ListKind>` (`pspad-list-kind`, outlined,
  **Tasks** / **Reference**, default Tasks), with **Add list** at the bottom
  left; Enter also adds. The panel closes, leaving the area board, and the
  new list sorts last there by creation date.
- Existing: the kind read-only (`pspad-list-kind-readonly`, `ListIcon` and
  its label — `Kind` is fixed at creation, so a list's screens never handle
  a mid-life change), an **Area** `MudSelect` (`pspad-list-area`) that sends
  `MoveTaskListToArea`, and **Delete list** at the bottom right, confirmed by
  `ConfirmDialog` and then going to the list's area. The name saves as it
  changes (`RenameTaskList`).

**List kind shows as an icon, not a label, everywhere a list is listed.**
`Components/ListIcon.For(list)` picks `Icons.Material.Outlined.Checklist`
for `Tasks` and `Icons.Material.Outlined.LibraryBooks` for `Reference`
(`.LabelFor` gives the matching `aria-label`/tooltip text: "Task list" /
"Reference list"). Used on area-board list cards, the list screen's header
and search results — one place decides the mapping so a future third kind
adds one icon, not N call sites.

**A `Reference` list's page and card render `ReferenceRow`s, never
`TaskRow`s.** `ListPage` and `ListCard` branch on `list.Kind`: a `Reference`
list has no Done/open split (an item is never finished) and no add-task FAB
action — its FAB item is "Add item," opening `ReferenceItemPanel` in Add
mode instead of `TaskDetailPanel`. `ReferenceRow` shows the item's name, a
star toggle and — when the item has fields — one small `MudChip` per field
on its second line, in field order, as many as fit: the chips wrap onto a
clipped line, so a chip is shown whole or not at all. A link
(`FieldKind.Link` that passes `LinkSafety`) is a chip with a link icon and
only its label, a real `<a>` opening the URL in a new tab; a path is a chip
with a folder icon and its label that copies the path (snackbar "Copied");
anything else reads `Label: value`, quantities normalized
(`FieldDisplay.NormalizeQuantity`), cut with an ellipsis. Tapping a chip
never opens the item. The empty-list and delete-confirmation
copy read "items" instead of "tasks" for a `Reference` list
(`DeleteWarning`).

**Sharing opens from the list panel's Details view as two nav rows.**
`ListDetailPanel` shows the name, the kind label, the owner's **Area**
`MudSelect`, then one `MudPaper` holding a `MudList` of rows: **Members**
(secondary text "Only you", "Nobody joined yet", "N joined", or for an
owner with a live invite "N joined · link M min left") and — owner only —
**Public snapshots** (secondary text "None live", "1 live link", "N live
links", or "Needs a connection" offline or when the request fails; the
summary loads once per list and tolerates failure). A row navigates to
`?view=members` or `?view=snapshots` (`ListPanelView`, `ListQuery`); the
sub-view replaces the Details body, takes the row's name as its title and
shows a back arrow (`DetailPanel.OnBack`) returning to `?view=`-less
Details. A non-owner asking for `?view=snapshots` falls back to Details.
On a phone the panel is full width, so each sub-view is its own full-screen
page.

**Members sub-view** (`Components/ListMembersView.razor`) branches on
ownership.

- **Owner.** `InviteLinkCard`, then **People · N** (owner plus members): a
  `MudList` of rows with initials avatar (`AvatarColor`), "You" for the
  owner, an **Owner** chip, and for each member their name, join date and a
  remove icon (`PersonRemove`, confirmed via `ConfirmDialog`). **Stop
  sharing · remove everyone** (confirmed) shows while there are members or a
  live invite.
- **Member.** A "Shared by {OwnerName}" card with a "You can edit" chip, the
  same people list without remove icons and "Only {OwnerName} can invite
  people.", the **Show in my area** `MudSelect` (filing, see below) and
  **Leave list** (`Color.Error`, confirmed, sends `LeaveTaskList` and
  navigates to "Shared with me"). A member never sees the invite link or
  code — the server does not sync them.

**`InviteLinkCard`** is a `MudPaper` titled "Invite link" with a state chip.
An invite is a link plus a 6-character code and lives 30 minutes
(`TaskList.InviteLife`). Four states:

- **None** (never shared): "Only you use this list", explanation, **Create
  invite link** (`ShareTaskList` with a fresh `InviteToken` and
  `InviteCode`).
- **Live:** chip "N min left" (re-rendered every 30 s), a
  `MudProgressLinear` and "Works until HH:mm" in the user's time zone, the
  `QrCode` of the join link, the read-only link field with **Copy** — both
  carry the code (`InviteCode.JoinLinkFor`, `#code=` fragment) — the code
  (`InviteCodes.Format`, "K7M-4PX") in its own card with its own copy
  button, **New link** (confirmed — the old link stops, members stay) and
  **End link now** (`StopSharingTaskList`).
- **Expired:** chip "Expired HH:mm", an info alert that nobody can join with
  the old link and code and the people already in stay, **Create link ·
  30 min**.
- **Closed:** chip "Closed", a warning alert that five wrong codes closed
  the link, **Create link · 30 min**.

Every action reloads the list from the replica so the card reflects the
fresh token and code.

**`QrCode`** renders a QR PNG (QRCoder, ECC level M) as a data URI in an
`<img class="pspad-qr">` inside a `MudPaper` with a hard-coded white
background, so the code stays dark on white in the dark theme too — a
scanner needs the contrast.

**A member's list panel hides owner-only controls; its own filing picker
takes their place.** `ListDetailPanel`'s kind icon/label render for owner
and member alike — only the owner's name field, **Area** `MudSelect` and
**Delete list** check `IsMine` (the panel's own `_list.UserId ==
State.UserId`, not `ListPlacement`). A member's **Show in my area**
`MudSelect` (in the Members sub-view, not on Details) takes the owner's
**Area** one's place: the member's own live
areas plus "Shared with me" (`null`), sending `PlaceList` on change and
preselecting whatever `ListView` already has on file for that
`(user, list)`. `ListPage`'s FAB Menu drops **Edit list** / **Delete list**
for a member, keeping **Add task**/**Add item**; `AreaBoard`'s FAB Menu
carries only the area's own actions (New list, Edit area, Delete area) —
it has no per-list items to drop. `ListCard`'s `⋯` menu (`ThingMenu`) keeps
**Edit** for everyone — it opens the panel, which restricts itself — and
drops only **Delete** for a list the viewer does not own (`OnDelete` left
unbound). `TaskDetailPanel`'s goal row shows only when the open task is
the viewer's own (`task.UserId == me`) — a member edits a shared task but
never its owner's goal.

**A shared list carries a marker wherever it is a card.** `ListCard` shows
a `People` icon in its actions when `List.IsShared` (members or an active
token), with a `MudTooltip` reading "Shared · N people" (one more than
`Members.Count`, the owner included) or "Shared · 1 person" for a list with
no joiners yet.

**"Shared with me" is a virtual area, not an `Area` document.** A
client-side constant (`State/SharedWithMe`, id
`0000000a-0000-0000-0000-000000000001`) for member lists nobody has filed
elsewhere (`ListPlacement`, `PlaceList`). It shows last: a `People`-icon
row in the desktop sidebar after the user's own areas (`NavSidebar`), the
last chip in the phone's `AreaChips`, and the areas index falls back to it
when the user owns no area but has a shared list. Its board
(`/areas/{SharedWithMe.AreaId}`, `AreaBoard`) is the same `MasonryGrid` as
any area, but carries no FAB — a member cannot create a list here — and an
empty board reads "Nothing is shared with you right now." instead of "No
lists yet."

**`/join/{token}`** (`Pages/JoinPage.razor`, signed-in only) asks for the
invite code. A `#code=` fragment (from the shared link or QR) that is well-formed fills it
in and joins at once; otherwise a card reads "Enter the invite code" with a
monospace code `MudTextField` and **Join list**, enabled once the code is
well-formed. Outcomes: success triggers a sync and navigates to
`/lists/{listId}`; a wrong link or code shows an error alert "That link or
code doesn't work." (one message for both — the server answers one 404);
an expired invite replaces the card with `EmptyState` "This invite has
expired" and "Invites work for 30 minutes."; too many tries (429) shows a
warning alert "Too many tries. Wait a few minutes and try again."; offline
shows `EmptyState` "Joining needs a connection." with "Try again".

**Public snapshots are the owner-only `?view=snapshots` sub-view.**
`Components/SnapshotPublisher.razor` offers expiry presets in a
`MudToggleGroup` (1 day / 7 days / 30 days) plus a **Date…** item that
opens a `MudDatePicker` capped at 365 days out; **Publish
snapshot** flushes the outbox first (`ISyncTrigger.SyncNowAsync` — a
snapshot is built from server state) and is disabled outright while
offline, with a caption explaining why. A published snapshot copies its
link to the clipboard and shows a success snackbar. Below, "Live links · N"
lists each active snapshot: days left (warning colour when urgent),
"Published {date}", chips "N ticked" / "No ticks yet" and "N entries", a
copy button and a QR toggle that expands the `QrCode`, **Copy link**,
**Open** and **Revoke** (`ConfirmDialog`, "The link stops working."); none
live reads "No live links. Published copies show up here." A custom date resolves to
the end of that day in the user's own time zone, not UTC midnight.

**`SnapshotMarkChip`** — a small outlined `MudChip`, "Marked on a
snapshot" — renders on a task, step or reference item's row and in its
panel whenever it carries at least one snapshot mark. The panel offers
**Dismiss**, sending `ClearTaskSnapshotMarks` or
`ClearReferenceItemSnapshotMarks`; completing the task itself is a
separate, deliberate action the chip never triggers.

**`/public/snapshot/{token}`** (`Pages/SnapshotPage.razor`, `PublicLayout`,
anonymous — outside `AppShell` like `/welcome`) is themed with the app's
palette. A slim top bar (brand mark, **Log in** or **Open PSPad** depending
on whether a local session exists); a hero `MudPaper` on `mud-theme-primary`
with "Public snapshot · shared by {OwnerName}" (owner omitted when blank),
the list's name, outlined chips "Copy from {date}" and "Link works until
{date}", and "N of M ticked by visitors" over a `MudProgressLinear`; an info
alert explaining that a tick is seen by everyone with the link and by the
owner. A `Tasks` snapshot shows **To do · N** (each open task a `MudPaper`
with its own `MudCheckBox`, star, due chip, a "ticked" chip, `MarkdownField`
`ReadOnly` description and indented steps with their own checkboxes), then
**Already done by {owner} · N** (struck-through names, no checkboxes); a
`Reference` snapshot lists items with their checkbox, description and fields
(`ReferenceFieldValue`). A footer reads "A read-only copy made with PSPad."
with a "What is PSPad?" link to `/welcome`. Ticks are optimistic, reverting
with a snackbar ("Couldn't save that tick. Try again.") on failure, and
disabled while offline. An unknown or expired token renders one
`EmptyState` ("This snapshot has expired or never existed.") with a link to
`/welcome` — the two cases are deliberately indistinguishable. Offline with
no cached copy shows a different `EmptyState` ("Connect to the internet to
open this snapshot."); offline with a cached copy shows the snapshot behind
a warning `MudAlert` ("Offline — showing the copy from {date}. Ticks are
paused.") with every checkbox disabled. A signed-in visitor's open is
recorded (`POST /api/me/snapshot-visits`) and the snapshot cached to
IndexedDB for that offline path.

**`/snapshots`** (`Pages/SnapshotsPage.razor`, signed-in) is **List
snapshots**: a `MudList` of every snapshot the caller has opened, newest
first, name plus "Expires {date} · Opened {date}", each row linking to
`/public/snapshot/{token}`. An empty list shows `EmptyState` ("Snapshots you open while
signed in show up here."). Online it reads `/api/me/snapshot-visits`;
offline, or on request failure, it falls back to the IndexedDB cache,
pruning expired entries from both sources before display. A sidebar row
(desktop, after Statistics) and an `AccountDrawer` row (phone, before
Settings) link here.

**Inbox items use the same shell.** `Layout/InboxItemPanel.razor` is
addressed as `?inbox=new` (the Inbox FAB or its empty state) or
`?inbox={itemId}` (tapping an item card), via `InboxQuery`.
- **Capture:** only a Name field, with **Add** at the bottom left; Enter
  also adds. The item keeps the time it was captured, which its card shows.
- **An existing item** opens as a convert-to-task form: Name, then a
  draft `StepList`, then `ListRow`, `DueDateRow`, `PriorityRow` and
  `GoalRow`, then a Description `PanelSection` holding `MarkdownField`,
  plus a star in the header — the same order as `TaskDetailPanel`. Draft
  steps and the description live only in the panel until conversion;
  closing without converting drops them, as it drops the other draft
  edits. Draft steps have no checkbox and no due date. The Name field is the item's own text and saves as it commits
  (`RenameInboxItem`), so an item can be reworded without being converted.
  - **Convert to task** on the left sends `OrganiseInboxItem` and then only
    the edits that differ from the defaults — one `AddStep` per draft step
    in order, and `SetTaskDescription` for a non-blank description — so one
    tap files a finished task.
  - **Discard** on the right removes the item.
  - The List row starts on the list used for the previous conversion
    (`AppState.LastInboxListId`), falling back to the first list.
- `PriorityRow`, `GoalRow` and `ListRow` are shared with `TaskDetailPanel`.
  `GoalRow` offers only In progress goals, but still names a linked goal
  that has since closed.
  The Inbox never uses a dialog to capture.

**Reference items use the same shell, addressed like a task.**
`Layout/ReferenceItemPanel.razor` is addressed as `?item=new&list={listId}`
(the list or area board's "Add item" FAB action) or `?item={itemId}`
(tapping a `ReferenceRow`), mirroring `TaskDetailPanel`/`TaskQuery`'s
`?task=` pattern. Header: X, star toggle on the right (`HeaderActions`, only
once the item exists — no star while still adding). An unboxed `Typo.h6`
name field sits under the header, same as a task's name. Below it, in every
mode (Add included), mirroring the task panel's content → divider →
property rows → Description order:

1. **Labels** — a `PanelSection` titled "Labels" holding `ReferenceFieldList`
   (the type and its fields stay named `ReferenceField`/`Fields` — only the
   UI wording changed), laid out like `StepList` (the same `pspad-step` rows,
   dense, no big buttons): each row a caption `Label` over a
   `ReferenceFieldValue` (rendering per its `FieldKind`, below), a Close
   icon removing it ("Remove {label}") and a `DragIndicator` handle as the
   last element — rows reorder by drag only (`MudDropContainer`, keyed on
   the field order), disabled while a field is being edited; a tap on the row body opens `ReferenceFieldEditor`
   (compact: label, value, display kind, Save/Cancel/Remove) in the row's
   place. The last row is the add row — a `+` lead and two unboxed dense
   fields, **Label** and **Value**. Enter in **Label** never adds — it moves
   focus to **Value**; Enter in **Value** sends `AddReferenceField` with an
   automatic display kind (blank label re-focuses Label instead of sending),
   clears both and puts focus back on Label; a rejection restores the typed
   text. Each field row carries the same 44px lead column, holding a muted
   icon for its kind, so labels line up with the add row. A click on a link
   or the copy button inside a row does not open the editor. The kind select
   lives only in the editor of an existing field.

   **Dropping onto the Value input** (add row, and the inline editor's Value
   input) accepts a dragged link or text: a `text/uri-list` payload fills it
   with the first non-comment (`#`) URI, `text/plain` otherwise, then focuses
   Value. A dropped local file cannot be filled — browsers never expose a
   file's full path — so the drop is prevented (no navigation, no upload) and
   an Info snackbar says so, directing Windows users to Shift+right-click →
   Copy as path. Implemented as a small JS module
   (`wwwroot/js/drop.js`, `attach(element, dotnetRef)`) imported and attached
   to the input's element reference on first render, calling back into
   `[JSInvokable] OnDropped(string? uriList, string? text, bool hadFiles)`;
   import or attach failure degrades silently (`ILogger` warning, never
   thrown, never `stderr`). Disposed with the component.
2. A `MudDivider`, then the property rows: the same **`ListRow`** the task
   panel uses (folder icon, "List", `Area › List`), offering only
   non-deleted `Reference` lists — an item never crosses into a `Tasks`
   list. Picking one sends `MoveReferenceItemToList`.
3. **Description** — a `PanelSection` holding the item's `MarkdownField`
   (see below).
4. Footer (existing items only): "Created {ddd, d MMM yyyy}" as a muted
   `Typo.caption` on the left — absent for an item stored before
   `CreatedAt` existed — and a red trash `MudIconButton` on the right, same
   layout and delete-confirmation pattern as the task footer.

As with a task, an existing item's fields save as they change — no Save
button outside Add. **Add mode holds a local draft** and sends nothing until
**Add item**: `ReferenceFieldList` is given a draft list instead of an item
(same rows, same add row — Enter there adds a draft field and never creates
the item), the list row starts on the target list and picking another one
changes where the item will be created, and the `MarkdownField` saves into
the draft. **Add item** (or Enter in the name) sends `CreateReferenceItem`;
once accepted it sends `AddReferenceField` for each draft field in order
(fresh ids, the draft's display hint), then `SetReferenceItemDescription`
if the draft description is not blank, then closes the panel — adding
never opens the new item. A rejected create shows a Warning snackbar and
keeps the whole draft; a rejected follow-up shows a Warning snackbar and
still closes, since the item exists. The draft resets when the target list changes or
the panel closes.

**Panel subsections share `PanelSection`.** `Components/PanelSection.razor`
is a `MudDivider` (`--pspad-line`) followed by a `SectionHeading`
(`pspad-panel-section-title`, the same caption style as page sections, not
overline or uppercase) over its content, with `px-3` padding. It
heads "Description" in both panels and "Labels" in the reference panel.

**Descriptions are a shared `MarkdownField`, on tasks and reference items
alike, always present and saved as focus leaves.** There is no
Add-a-description button and no Save/Cancel. An empty, editable description
shows an outlined multi-line `MudTextField` directly (3 lines, auto-sizing,
placeholder "Add a description…"). A filled one renders `Value` through
`MarkdownRenderer.ToHtml` (Markdig, `DisableHtml()`, pipe tables, task lists
with checkboxes disabled, links restricted to `http`/`https`/`mailto`,
`target="_blank" rel="noopener noreferrer"` added to safe links) inside an
outlined `MudPaper` with a small edit `MudIconButton` top-right; a click on
the rendered text, or the icon, opens the raw text, focused. A click on a
link inside the text follows the link and does not open the editor — the
click is read through a custom `markdownviewclick` event
(`Markdown/EventHandlers.cs`, registered in `wwwroot/PSPad.App.lib.module.js`)
whose `OnLink` says whether the target sits inside an `<a>` and
`HasSelection` whether text is selected — a drag-selection does not open the
editor either. If the registration throws, the initializer only warns, so
the app still boots and the edit icon remains the way in. Leaving the
field (blur) calls `OnSave` if the text changed and returns to the rendered
view; unchanged text sends nothing. Escape discards the draft and returns to
the view. If the caller rejects the save or it throws, the field stays in
edit mode with the typed draft, so a rejected write never silently discards
what was typed; the editor takes focus back and the panel shows the
Warning snackbar. The draft survives an
outside `Value` change while editing. Closing the `DetailPanel` by overlay
click blurs the field first, so an open edit is saved rather than dropped.
`ReadOnly` and `Disabled` never edit: a filled value shows rendered, an
empty one reads "No description".

**Field display kinds are detected, not chosen by default, and stay a
client-side hint.** `State/FieldDisplay.Of(field)` reads the field's stored
`Display` hint if present and valid, otherwise detects one from the value:
an `http(s)://` value is `Link`, a path-shaped value (`C:\`, `\\`, `/`,
`~/`) is `Path`, a `<number><unit>` value (`2.5kg`, `-10 C`) is `Quantity`,
anything else is `Text`. `ReferenceFieldEditor` lets a field's display be
overridden explicitly, stored back as the same lowercase-name hint
(`FieldDisplay.HintFor`) — the domain never interprets it (`adr/0047`).
`ReferenceFieldValue` renders per kind: `Link` as a `MudLink` opening in a
new tab when the scheme is safe (plain text otherwise), `Path` as `<code>`
with a copy `MudIconButton` next to it, `Quantity` normalized to `"<number>
<unit>"` spacing, `Text` as pre-wrapped plain text. The copy button
(`pspad-field-copy`) calls the `Clipboard` service and shows a snackbar —
`Severity.Success` "Copied" on success, `Severity.Warning` "Could not copy —
select the path instead." on failure (clipboard permission denied or
unavailable).

**Empty states share one component.** A page or board with no items yet
shows `Components/EmptyState.razor` as the first cell of its grid, sized
like one card (`MudItem xs="12" sm="6" lg="4" xl="3"`): an outlined
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

- **Zero actions** → no FAB — "Shared with me" is this case: a member
  cannot create a list there.
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
| Area | New list, Edit area & order lists (→ area panel), Delete area | FAB Menu |
| Goals | Add goal (→ new-goal panel) | plain `MudFab` |
| Goal | Edit goal (→ goal panel), Delete goal | FAB Menu |
| List | Add task (→ new-task panel) or Add item (→ new-item panel) for a `Reference` list, plus Edit list (→ list panel) and Delete list for the owner | FAB Menu |
| Inbox | Capture (→ capture panel) | plain `MudFab` |
| My Day, Settings, Statistics | none | no FAB |

Below `md`, the page FAB is a Material-3 tonal container — a 24%
primary/surface mix with a primary-coloured icon — rather than a solid
accent circle; the raised My Day button (`BottomNav`) is the only solid
accent circle on a phone.

`+ New area` stays pinned in the sidebar — it is not a page's own action,
it belongs to the sidebar's area list. Item-level rename/delete stays on
the item's own `⋯` (`ThingMenu`) wherever the item is a card or row inside
a page, e.g. `ListCard`'s and `GoalCard`'s own menus on `AreaBoard` and
`GoalsPage` — those are untouched by the page-level rule above.

Deleting an area or a list takes its contents with it (`adr/0042`), so its
`ConfirmDialog` counts what goes: "Delete “Dom” and its 2 lists and 5
tasks? This can’t be undone." Every delete entry point (the area FAB Menu,
the area panel, a `ListCard`'s menu, the list FAB Menu) builds the text with
`State/DeleteWarning`, counting live children from the replica; an empty
container drops the "and its …" clause.

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

**Surface tokens.** `wwwroot/css/app.css` defines CSS custom properties on
`:root`, overridden under `[data-theme="dark"]` on `<html>`. `index.html` sets
`data-theme` before Blazor boots so the first paint is right;
`ThemePreference` resets it to the effective mode on every initialise
(including an OS flip in System mode) and every mode change, so the tokens
follow a switch without a reload:

| Token | Light | Dark | Use |
|---|---|---|---|
| `--pspad-line` | `rgba(0,0,0,.08)` | `rgba(255,255,255,.07)` | card and panel edges, dividers |
| `--pspad-line-strong` | `rgba(0,0,0,.16)` | `rgba(255,255,255,.14)` | 2× `--pspad-line`; the empty state's dashed border |
| `--pspad-hover` | `rgba(0,0,0,.04)` | `rgba(255,255,255,.04)` | row hover, count pills, inactive chips |
| `--pspad-tint` | primary at 12% over transparent | same | selected and active fills, icon tiles (the sidebar's active link) |
| `--pspad-raised` | `#FFFFFF` | `#1B1B1B` | card surface |
| `--pspad-card-shadow` | `0 1px 2px rgba(0,0,0,.06)` | `0 1px 2px rgba(0,0,0,.3)` | card elevation |

`PSPadTheme.LightRaised` and `DarkRaised` hold the raised values in C# so the
contrast tests can use them; a test keeps them in sync with `app.css`.
`LayoutProperties.DefaultBorderRadius` is 12px.

**Decorative lines are exempt from the 3:1 rule.** `LinesDefault`,
`TableLines` and the divider colour keep their 3:1 line: inputs, checkboxes
and toggles must stay identifiable. `--pspad-line` is only for card edges,
panel edges and dividers, which are told apart by surface and spacing, so it
is exempt. Text and icons keep 4.5:1 / 3:1, asserted for every preset and
awkward custom accent, light and dark, in `PSPadThemeTests`:
- selected chip or toggle text, `TextPrimary` on `--pspad-tint`: 4.5:1;
- round checkbox outline, `ActionDefault` on `--pspad-raised`: 3:1;
- unstarred star, `TextSecondary` on `--pspad-raised`: 3:1;
- muted meta text, `TextSecondary` on `--pspad-raised`: 4.5:1.

**Surfaces.** Every `MudPaper Outlined="true"` is `--pspad-raised` with a
`1px solid var(--pspad-line)` border, 12px radius and `--pspad-card-shadow`.
Clickable cards (`pspad-day-task`, `pspad-task-card`, `pspad-goal-card`,
`pspad-inbox-card`) take a stronger border on hover and on keyboard focus
(`:focus-visible`, or a focused control inside); `.pspad-row` likewise takes
`--pspad-hover` on hover and on keyboard focus within. Inside a `MudDrawer`
outlined papers and expansion panels sit on `--mud-palette-surface` with no
shadow, so they do not look sunken below the drawer. `MudExpansionPanels`
(a list's Completed, a goal's Completed, Today's Completed, the Statistics
feed) is one rounded card with a soft border and no hard rules; `MudDivider`
inside cards and panels uses `--pspad-line`. Outlined inputs are 10px radius;
`MudButton` (not icon buttons) and `MudChip` are pills (`999px`), except the
`WeekStrip` day buttons (12px) and the card's "Show all" text link. More
specific rules outrank the card look: the phone `WeekStrip` stays edge to
edge (no radius, bottom border only, no shadow) and `GoalSummaryCard` keeps
its hover and focus wash. The card icon tile deepens its tint to 20% on hover
and shows a 2px primary outline on keyboard focus.

**Chips and toggles.** `AreaChips` are pill chips without outline: the
current area `--pspad-tint` with `TextPrimary` and medium weight, the rest
`--pspad-hover` with `TextSecondary`, "New area" a text-style primary chip.
`MudToggleGroup` (list kind, task kind One-time / Repeating, Statistics days,
snapshot days) is a pill whose selected item is `--pspad-tint` with
`TextPrimary`.

**Panel rows and empty states.** The `PropertyRow` activator has an 8px
radius and `--pspad-hover` on hover. `EmptyState` puts its icon in a 48px
circle of `--pspad-tint`; the dashed border uses `--pspad-line-strong` so it
still reads as a target.

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
| `Typo.h5` as `h1` | page title, 20px, `TextPrimary`, weight 600, with a muted 12.5px subtitle under it |
| `Typo.h6` as `SectionHeading` | section heading: 13px, weight 600, `h2`, count as a muted pill |
| `Typo.subtitle2` | card header link (list name, goal name), 14.5px medium |
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

**Sidebar**, top to bottom, one navigation tree at `md`+: a
non-interactive `AccountBadge` (avatar, display name, email — a label, not
a control), then a nav group of **My Day / Inbox / Goals / Statistics /
List snapshots**,
divider, the user's areas in `Position` order, then **Shared with me**
(`People` icon) when the user has a member list, then **+ New area**,
divider, **Settings** / **App info**, then a spacer, then a footer
(connection status, current date/time, "PSPad · GPL v3"). There is no search field in
the sidebar (temporarily unreachable from the UI, tracked as a known gap,
not a page to recreate speculatively) and no dropdown on the account badge.
Below `md` there is no sidebar: phones navigate through `MobileTopBar` and
`BottomNav` instead, with the sidebar's account badge, Settings, App info
and footer moved into `AccountDrawer` (`adr/0050`).

At `md`+ the sidebar is still `MudNavMenu`/`MudNavLink`, styled through a
`pspad-nav` class: 12px padding inside the drawer, links with an 8px radius
and 2px gap, outlined icons (`Icons.Material.Outlined.*`), counts as a
right-aligned muted number (not in the label) and a muted "Areas" caption
above the areas group. The active link is tinted with the primary at 12%
(`color-mix` on `--mud-palette-primary`, so every accent and dark mode
follow) at medium weight; its text uses `--mud-palette-text-primary` and
only its icon is primary, to hold the 4.5:1 contrast rule on the tint
(`PSPadThemeTests.TheActiveSidebarLinkReadsOnItsTint`).

**Routes:**

| Route | Screen |
|---|---|
| `/` | My Day |
| `/inbox` | Inbox |
| `/areas` | opens the last-used area on this device, else the first; empty state when there are none |
| `/areas/{areaId}` | area screen — list cards |
| `/areas/shared` | the same screen for `SharedWithMe.AreaId` — "Shared with me", no FAB |
| `/join/{token}` | join a shared list by its invite token, then opens it |
| `/lists/{listId}` | list screen |
| `/goals` | Goals |
| `/goals/{goalId}` | goal screen — every task of one goal |
| `/snapshots` | List snapshots — public snapshots the caller has opened while signed in, newest first |
| `/public/snapshot/{token}` | a public snapshot, anonymous, outside `AppShell` |
| `/statistics` | Statistics — tiles, charts, Consistency heatmap and Inbox-captures bar chart, collapsed record feed |
| `/history` | redirects to `/statistics`, for bookmarks predating the rename (`adr/0038`) |
| `/settings` | Settings (account + change password + sign-out; application settings: time zone, theme, accent; sync status; delete account) |
| `/app-info` | version, license, docs/repo links |
| `/search` | search results (currently unreachable from the UI) |
| `/welcome` | public, signed-out landing screen |
| `/authentication/{action}` | OIDC login/logout flow, branded fragments |
| `?task={taskId}` | task detail overlay, on any of the above |
| `?area={areaId}`, `?area=new` | area detail overlay, on any of the above |
| `?goal={goalId}`, `?goal=new` | goal detail overlay, on any of the above |
| `?inbox={itemId}`, `?inbox=new` | inbox item overlay, on any of the above |
| `?item={itemId}`, `?item=new&list={listId}` | reference item detail overlay, on any of the above |

**Signed-out visitors land on `/welcome`**, not a bare login redirect.
Sign-out ends the Keycloak session directly rather than only clearing local
state. The boot splash is held — no application chrome renders — until the
first screen is ready to draw: the signed-in shell once its data is loaded,
or the welcome and sign-in screens. Each of those renders
`Components/BootSplashRelease`, which tears the splash down; a new layout
must render it too, or the splash covers it until `index.html`'s 20-second
fallback (ADR-0044). See `specs/backend-spec.md` §6 for the session decision
itself.

**Never build a second settings surface.** Account-level config (time zone,
theme, sign-out, account deletion) belongs on `/settings`. Per-item actions
belong on the item (`⋯` menu, or the page's own FAB per §3). There is no
third pattern.

**Sign-out lives inside the Account card**, under the avatar/name/email
block — not a standalone button elsewhere on the page.

**Change password is a link out, beside sign-out.** PSPad keeps no
passwords, so the Account card's "Change password" button opens Keycloak's
own Account Console (`{authority}/account/`, the console root — its
sub-page routes shift between Keycloak versions) in a new tab, riding the
existing browser session. No in-app `kc_action=UPDATE_PASSWORD` round trip:
it would re-enter the login-callback route ADR-0027 guards.

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
