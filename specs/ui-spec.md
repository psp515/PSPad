# UI Spec

Standing rules for building `PSPad.App` screens so every page looks and
behaves like it belongs to the same product. This is a rulebook, not a
history — it states what the client does now and how to extend it
consistently. For *why* a given rule exists, the superseded design
narratives (`ui-ux-redesign-design.md`, `ui-redesign-2-design.md`,
`ui-polish-design.md`) and the ADRs they cite are in git history
(`git log -- specs/`); the ADRs themselves stay in `adr/` and remain the
decision record where a rule traces back to one.

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

One column on a phone, two on a tablet, three inside the app's
`MaxWidth.Large` container, four once the viewport passes MudBlazor's `xl`
breakpoint (1920px). `sm`/`md`/`xl` (600px/960px/1920px) match
`BrowserViewport`'s own `Breakpoint.MdAndUp` split used for the sidebar, so
the grid and the shell agree on where "wide enough" starts. Row-flow, not
column-flow — DOM order stays reading order for keyboard and screen-reader
navigation.

Applied on: `AreaBoard` (list cards), `GoalsPage` (goal cards, active and
achieved separately), `Today` (overdue, today, tomorrow, goals in progress,
completed and upcoming each as their own grid), `InboxPage`, `ListPage` (open and completed separately),
`SettingsPage` (Account, Time zone, Theme, Sync, Danger zone each their own
card), and both skeleton components (`RowSkeleton`, `CardSkeleton`).

**Spacing scale.**

| Use | Value |
|---|---|
| Grid gap between cards/rows | `Spacing="4"` on `MudGrid` |
| Card interior padding | `pa-3` |
| Row interior padding | `px-3 py-2` |
| Space below a page title | `mb-4` |
| Space between stacked sections | `mt-4` / `mb-4` |

**Containers.** Page content sits in the app's `MaxWidth.Large` container
(set once in `AppShell`) — individual pages never set their own max width.

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
**Today**, **Tomorrow**, **Goals in progress**, then one `MudExpansionPanels`
holding **Completed (N)** and **Upcoming (N)**, both collapsed by default.
Every section hides when empty, except Today, which says "Nothing due
today." when Overdue is empty too. Upcoming groups its rows under a muted
caption per day (`DueDateRow.Describe`). Membership comes from
`TodayRule.Plan`, never from the page. A recurring row ahead of today
ticks the occurrence on its own day, not today's. Goals in progress are
`GoalSummaryCard`s ordered by due date, undated last, and open
`?goal={id}`.

**Shared row/card components, never duplicated per screen.** One
`TaskRow` renders in My Day, list cards, the list screen and search
results. One `ListCard`, one `GoalCard`, one `InboxItemCard`. A single
component per concept means a rule like never-overdue-for-recurring-tasks
cannot drift between the screens that display it.

**Task detail is an overlay, addressed by query string.** Clicking a task
appends `?task={taskId}` to the current route; `TaskDetailPanel` renders as
a slide-in overlay (full-screen below `md`) without reflowing the page. The
query string, not component state, so back-navigation closes the panel
without leaving the screen, and a task is linkable. Adding a task uses the
same panel in its new-task mode, addressed as `?task=new&list={listId}`
(`TaskQuery.ForNewTask`) — never an inline field or a dialog.

**Detail panels share one shell.** `Components/DetailPanel.razor` is the
only right-anchored detail drawer: 360px from `md` up, full width below it.
Its header row holds an X close button top-left, a title (`Typo.h5` from
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
   - **Due** (`DueDateRow`) — Today / Tomorrow / In 2 days / Next week (the
     next Monday), each with its date, then "Pick a date…" opening a
     `MudDatePicker` dialog; the value reads relatively (Today, Tomorrow,
     Yesterday, `ddd, d MMM`), in `Color.Error` when overdue.
   - **Repeat** (`RecurrenceEditor`, outside Add) — Daily, Weekdays, Weekly
     on today's weekday, Monthly on today's day, Never; a repeating task
     shows its last seven occurrences as chips under the row.
   - **Priority** — the four fixed levels with coloured dots.
   - **Goal** — the user's goals.
   - **List** (outside Add) — lists grouped under area headings; picking
     one moves the task at once. There is no Move button.
   Room for later task fields (note, reminders) goes under the rows.
5. Footer — "Created …" on the left, a red trash `MudIconButton` on the
   right that asks via `ConfirmDialog` before deleting. In Add the footer
   holds only **Add task**.

**Area and goal detail use the same shell.** `Layout/AreaDetailPanel.razor`
is addressed as `?area=new` (sidebar **+ New area**) or `?area={areaId}`
(the area's **Edit area** FAB item), via `AreaQuery`.
`Layout/GoalDetailPanel.razor` is addressed as `?goal=new` (the Goals FAB,
or the empty state) or `?goal={goalId}` (a goal card's name, or its
**Rename**), via `GoalQuery`. Both open with an outlined **Name** field
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
  the goal panel. An in-progress goal (My Day only) gets a primary accent
  and a flag icon, and adds a "Due …" caption (`Color.Error` once passed,
  omitted when undated) and a thin `MudProgressLinear` when tasks are
  linked.
- New: **Add area** / **Add goal** sits on the left of the footer, and
  Enter also adds. A new area then opens its screen; a new goal closes the
  panel.
- Existing: fields save as they change, with no Save. **Delete area** /
  **Delete goal** sits on the right and asks via `ConfirmDialog` first.
  Deleting an area goes home; deleting a goal closes the panel.

Areas and goals are never created or renamed through `NameDialog`.

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
| List | Add task (→ new-task panel), Rename list, Delete list | FAB Menu |
| Inbox | Capture (→ capture panel) | plain `MudFab` |
| My Day, Settings, History | none | no FAB |

`+ New area` stays pinned in the sidebar — it is not a page's own action,
it belongs to the sidebar's area list. Item-level rename/delete stays on
the item's own `⋯` (`ThingMenu`) wherever the item is a card or row inside
a page, e.g. `ListCard`'s and `GoalCard`'s own menus on `AreaBoard` and
`GoalsPage` — those are untouched by the page-level rule above.

---

## 4. Visual & theming

**Palette.** Defined once in `Theme/PSPadTheme.cs`, never hardcoded as a
hex literal in a component. Sage green as an accent on near-neutral
grounds, not a tint across the whole interface:

| Token | Light | Dark |
|---|---|---|
| Primary | `#4E7A5E` | `#8FBF9F` |
| Secondary | `#6E8F7C` | `#7FAE94` |
| Error | `#B3261E` | `#F2A9A2` |
| Warning | `#B26A00` | `#E0B252` |
| Background | `#F7F8F5` | `#141815` |
| Surface | `#FFFFFF` | `#1C211D` |
| Drawer/Appbar background | `#EDF1EA` | `#171C18` |
| TextPrimary | `#1E2A22` | `#E4E9E4` |
| TextSecondary | `#66736B` | `#94A199` |

Theme is **System / Light / Dark**, per device, held in `localStorage` via
`ThemePreference` — never on the `User` aggregate. It lives in
`SettingsPage`, not the account badge or any menu (a control nested in a
menu item is not reliably keyboard-reachable), picked from a `MudSelect`
list — the same dropdown pattern as the time zone picker below it, not a
button group.

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
the sage palette for free — the History screen's burndown chart is the one
example.

---

## 5. Navigation & auth screen shapes

**Sidebar**, top to bottom, one navigation tree at every width: a
non-interactive `AccountBadge` (avatar, display name, email — a label, not
a control), then a nav group of **My Day / Inbox / Goals / History**,
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
| `/history` | History + burndown chart |
| `/settings` | Settings (account + sign-out, time zone, theme, sync status, delete account) |
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
