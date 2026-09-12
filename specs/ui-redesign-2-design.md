# Sidebar UI — Design

**Supersedes:** the navigation decisions of `ui-ux-redesign-design.md` (D1, D2,
D4, D6, D8) and ADR-0013. The theme decision (D7 there) survives unchanged.

## Problem

The shell delivered by plan 09 splits navigation in two: app sections on one
surface, user areas on another, with different structures above and below
`md`. Two navigation trees exist and are maintained separately. The split FAB
is the only way to create anything, so creating a task means reaching for a
control in the opposite corner from the list it lands in. Areas are a
scrolling chip row that carries no sense of what is inside them, and nothing
shows an area's lists together.

The maintainer's judgement, holding it next to Microsoft To Do: it is hard to
use.

## Goals

One navigation tree at every width. An area that shows its lists and their
work without a click per list. Creation offered where the thing is created.
Colour that is ours, in both light and dark. Nothing above the presentation
layer moves.

Out of scope: any change to commands, events, aggregates, the event log or
sync. `PSPad.Module.Tasks` is not referenced differently and not edited, so
AD-3 and AD-4 are untouched and AGENTS.md §5 does not change.

## Decisions

### D1 — One navigation tree, revealed by breakpoint, not rebuilt

The sidebar is the whole navigation, at every width. At `md`+ it is permanent.
Below `md` the same component is a temporary drawer behind a hamburger in the
app bar.

Top to bottom: account avatar with name and email, search field, **My Day**,
**Inbox**, a divider, the user's areas in `Position` order, and **+ New area**
pinned to the bottom.

The bottom bar and the area bottom sheet are deleted. So is the flat areas
page, whose job the sidebar now does.

**Supersedes ADR-0013.** That record kept sections and areas apart on the
grounds that areas are unbounded while sections are a fixed set, and that a
phone has room for three or four primary destinations. The premise was
right and the conclusion was wrong: a drawer is not a bottom bar and has no
slot limit, so an unbounded list of areas costs nothing there. What the split
actually bought was two trees to keep in step, and a phone layout that could
not mirror the desktop one.

Both branches still live in the DOM at once, separated only by MudBlazor's
display utilities (`d-none d-md-flex`, `d-md-none`) — resolved by stylesheet
before first paint, so no flash, and no `IBreakpointService` round trip.
`MudHidden` remains rejected for the reason ADR-0013 gave.

### D2 — The hamburger comes back, below `md` only

At `md`+ the sidebar is permanent and there is nothing to toggle, so the app
bar carries no menu button. Below `md` the app bar shows the hamburger and the
title of the current screen.

### D3 — An area is a screen of list cards

Clicking an area in the sidebar opens `/areas/{areaId}`: one card per list in
the area, in `Position` order, then **+ New list**.

The sidebar does not expand areas into lists. Two levels of tree in a drawer
that is itself temporary on a phone reads badly, and the lists have somewhere
better to be.

### D4 — A list card previews up to ten open tasks and collapses

The card header carries a chevron, the list name and the count of open tasks.
The body shows at most **ten** unchecked tasks, each with a working checkbox.
Completed tasks never appear in a card. Past ten, a **Show all (N)** row links
to the list screen.

The chevron collapses the card to its header alone. Collapsed state is per
list, in `localStorage`, keyed by list id — a device preference about a
display, exactly like the theme, and by the same argument as ADR-0013's it
does not belong on an aggregate.

**Rejected: unbounded cards.** An area holding a seventeen-item list becomes
one long scroll and stops being an overview.

### D5 — The task detail panel is an overlay, addressed by query string

Clicking a task appends `?task={taskId}` to the current route. The panel
slides in from the right over the content, with a shadow; it does not reflow
the layout. Below `md` the same component fills the screen.

The query string, not component state: the browser's back button closes the
panel instead of leaving the list, and a task is linkable. It also means Today,
Inbox, an area screen and a list screen all open the same panel with no
per-screen wiring.

The panel holds: checkbox, name, star; steps with their own checkboxes and
**+ Next step**; due date; recurrence; goal; priority; the owning list; created
date; delete.

**Rejected: a permanent third pane.** It reflows every screen at every width
and buys visibility of a list the user has just clicked away from.

### D6 — Creation is offered where the thing is created

The FAB is deleted, with `FabContext` and `FabAction`.

| Thing | Where |
|---|---|
| Task | **+ Add task** at the foot of a list card and of the list screen |
| List | **+ New list** at the foot of an area screen |
| Area | **+ New area** pinned to the foot of the sidebar |
| Inbox capture | a permanently open field at the top of the Inbox |

Areas and lists open a small dialog for their name; tasks and captures are
inline fields that commit on Enter.

**Rejected: keeping the FAB alongside.** Two routes to the same act, one of
which is always in the wrong corner.

### D7 — Renaming, reordering and deleting live in a `⋯` menu on the thing

Areas carry it in the sidebar row; lists carry it in the card header and the
list screen's title. There is no management screen.

### D8 — My Day stays flat and unfiltered

No cards, no area chips, no filter. Overdue tasks are pinned in their own
section at the top, which disappears entirely when nothing is overdue. Then
today's tasks, then a collapsed **Completed** section.

AGENTS.md §3 makes Today the one screen that answers "what do I do now" across
all areas; a filter there would undo it. Recurring tasks are never overdue,
and the rule lives in the domain layer, so the row renders whatever
`TodayRule` says and does not re-decide.

### D9 — Organising an inbox item expands it in place

Tapping an item opens name, target area and target list inside the row, with
**Move**. No dialog. Ten captured items are ten quick interactions.

Carried over unchanged from the previous spec's D9.

### D10 — One task row, everywhere

`TaskRow` renders in My Day, the Inbox, list cards, the list screen and search
results. Checkbox, name, and a metadata line carrying only what applies: list
name (suppressed where the screen already names the list), due date, step
progress, priority dot, recurrence glyph, star. Overdue styling comes from the
theme's error colour.

One component means the never-overdue rule cannot drift between the five
screens that display it.

### D11 — Search filters the local replica

The sidebar field filters the IndexedDB replica by task name and list name and
routes to `/search?q=`. Results group tasks and lists, showing each task's
`area › list` path.

No endpoint, no Mongo index, no command — so it works offline like everything
else, and it does not turn a presentation change into a backend one. A
server-side search over full text is a separate subsystem if it is ever
wanted.

### D12 — Sage palette, custom in both modes

MudBlazor's defaults are replaced. Green is an accent on near-neutral grounds
rather than a tint across the whole interface.

| Token | Light | Dark |
|---|---|---|
| Primary | `#4E7A5E` | `#8FBF9F` |
| Secondary | `#6E8F7C` | `#7FAE94` |
| Background | `#F7F8F5` | `#141815` |
| Surface | `#FFFFFF` | `#1C211D` |
| Drawer / AppBar background | `#EDF1EA` | `#171C18` |
| Lines | `#DCE3D9` | `#2A312C` |
| TextPrimary | `#1E2A22` | `#E4E9E4` |
| TextSecondary | `#66736B` | `#94A199` |
| Error | `#B3261E` | `#F2A9A2` |
| Warning | `#B26A00` | `#E0B252` |
| Success | `#4E7A5E` | `#8FBF9F` |

Theme selection stays System / Light / Dark in `localStorage`, per device,
exactly as ADR-0013 decided. It moves out of the app bar into the account
menu.

### D13 — The account menu carries everything that is not task navigation

The avatar is the first letter of the user's email on a colour derived
deterministically from the user id, so the same user is the same colour on
every device. Clicking it opens: **Goals**, **History**, **Theme**
(System / Light / Dark), **Sync** (pending command count), **Sign out**.

Goals and History keep their routes and their screens; they lose their
permanent sidebar rows because they are low-frequency and the sidebar's job is
areas.

### D14 — Sidebar counts are computed once and refreshed on command

The counts beside My Day and Inbox are held in `AppState`, computed at startup
and recomputed after any command that could change them. They are not derived
during render.

Recomputing per render means walking every task in the replica on every
navigation. At one user's scale it is invisible; the point is that it is
invisible for the wrong reason, and the cheap version is no harder to write.

## Structure

### Routes

| Route | Screen |
|---|---|
| `/` | My Day |
| `/inbox` | Inbox |
| `/areas/{areaId}` | area screen — list cards |
| `/lists/{listId}` | list screen |
| `/search?q=` | search results |
| `/goals`, `/history` | unchanged |
| `?task={taskId}` | detail panel, on any of the above |

`/areas` and `/tasks/{id}` are removed.

### Files

**Added:** `Layout/AppShell.razor`, `Layout/AccountMenu.razor`,
`Layout/TaskDetailPanel.razor`, `Components/ListCard.razor`,
`Components/TaskRow.razor`, `Components/NameDialog.razor`,
`Pages/AreaBoard.razor`, `Pages/SearchPage.razor`,
`State/CardCollapseState.cs`, `State/AvatarColor.cs`, `State/SidebarCounts.cs`,
`State/ReplicaSearch.cs`, `State/SearchHit.cs`, `State/TaskQuery.cs`, and
`test/PSPad.App.Tests/AppTestHost.cs` as the one arrangement helper for
component tests.

**Rewritten:** `Layout/NavSidebar.razor`, `Theme/PSPadTheme.cs`,
`Pages/Today.razor`, `Pages/InboxPage.razor`, `Pages/ListPage.razor`,
`wwwroot/css/app.css`.

**Deleted:** `Layout/MainLayout.razor` (becomes `AppShell`),
`Layout/AppFab.razor`, `Layout/BottomNav.razor`, `Layout/AreaSheet.razor`,
`Layout/ThemeToggle.razor`, `Pages/Areas.razor`, `Pages/TaskDetail.razor`,
`State/FabContext.cs`, `State/FabAction.cs`.

**Untouched:** everything under `State/` and `Sync/` other than the two FAB
types, the additions above, and one event added to `CommandSender` so the
sidebar counts know when to recompute; `Api/`; `Components/StepList.razor`;
`Components/RecurrenceEditor.razor`; every project outside `PSPad.App`.

### Plans

**Plan 10 — shell.** Palette, `AppShell`, the sidebar with account menu and
search field, the hamburger, the routes, and the deletion of the old
navigation and the FAB. Ends with every existing screen reachable and
rendering inside the new shell.

**Plan 11 — screens.** `TaskRow`, `ListCard`, `TaskDetailPanel`, then My Day,
Inbox, the area screen, the list screen and search drawn against the shell.

Shell first: the palette and the detail panel are cross-cutting, and settling
them once means each screen is written once.

## Testing

bUnit component tests, `[UnitTest]`, following plan 07's conventions:
`Render<T>()`, and `JSInterop.Mode = JSRuntimeMode.Loose` because MudBlazor's
inputs call into `mudKeyInterceptor`.

What is asserted:

- the sidebar lists every app section and every area, and raises a selection
- **+ New area** is present regardless of how many areas exist
- a list card shows at most ten unchecked tasks, and no completed ones
- **Show all (N)** appears only when the list holds more than ten open tasks
- collapsing a card survives a re-render, and the state is keyed by list id
- a recurring task never renders overdue styling, on any screen that shows it
- `?task=` opens the panel; removing it closes the panel and not the screen
- search matches tasks and lists by name and reports each task's area and list
- the account menu offers Goals, History, Theme and Sign out
- the avatar colour is stable for a given user id

What is not asserted: palette values, spacing, and which breakpoint branch is
visible. The first two are judgement, and pinning them only makes changing
them expensive. The third is invisible to bUnit, which applies no stylesheet —
breakpoint behaviour is verified by hand in a browser, as ADR-0013 established
and ADR-0014 keeps.

## Out of scope

Server-side search. Drag-and-drop reordering (the `⋯` menu moves items).
Anything behind AGENTS.md §2's later subsystems. Any change to a command,
event, aggregate, index, endpoint or sync path.

## Open

Whether an area screen should offer a combined "all tasks in this area" view
alongside the per-list cards. Deferred: the list cards answer it for now, and
adding the view later costs one route.
