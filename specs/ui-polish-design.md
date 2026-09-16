# UI polish and burndown — Design

**Amends:** `ui-redesign-2-design.md` D12 (theme's home) and D13 (History's
home). Everything else in that spec stands.

**New records:** ADR-0019 (`CreatedAt` on `TodoTask`), ADR-0020 (History as a
sidebar row, theme into Settings).

## Problem

Five defects the maintainer hit driving the app, plus one capability they
asked to pull forward.

1. The boot screen is the stock Blazor template — two unstyled `<circle>`
   elements and a `loading-progress-text` div, neither of which `app.css`
   styles. There is a second, different loader inside `AppShell`
   (`MudProgressCircular`) once WASM is up.
2. After sign-in the account control renders as a bare `?` on a coloured
   circle with no text beside it. `/api/me` returns a GUID as the display
   name.
3. There is no settings screen. Theme and History live only in a menu hung
   off that unreadable `?`. `SetUserTimeZone` has existed since slice 1 with
   no caller, so every user is on `Etc/UTC`.
4. Every screen is a single column, however wide the window.
5. Screens render their empty state — "Nothing due today." — during the
   `await` in `OnInitializedAsync`, before any data exists.
6. No analytics of any kind. AGENTS.md §2 put them at subsystem 8, *later*.

## Goals

Correct identity data. A settings screen that makes the time zone reachable,
which is what makes the Today rule correct. A loading experience that is one
brand mark from first paint to first screen, and a skeleton instead of a lie
on every screen. Columns that use the width. A burndown chart that works
offline like everything else.

Out of scope: server-side analytics endpoints, per-area breakdowns, streaks,
habits, push reminders, drag-and-drop reordering.

## Decisions

### D1 — One brand mark, from first paint to first screen

`wwwroot/index.html` carries the `brand/icon.svg` mark inline: the sage tile,
the page, and the check path `M184,272 l48,48 l96,-120` drawn by a looping
`stroke-dasharray` / `stroke-dashoffset` animation. The CSS is a `<style>`
block in `<head>`, so it paints before `MudBlazor.min.css` and before WASM.

A short inline script reads `localStorage["pspad.theme"]` — the key
`ThemePreference` already writes — and stamps `data-theme` on `<html>` before
first paint, falling back to `prefers-color-scheme`. Without it a dark-mode
user gets a white flash for the length of a WASM download.

`Components/BrandLoader.razor` renders the same mark inside the app, and
`AppShell` uses it where it used `MudProgressCircular`. Boot and shell are
then visually continuous instead of two unrelated spinners.

**Rejected: a progress ring driven by `--blazor-load-percentage`.** More CSS
to get right in two themes, for a number that is only meaningful on a cold
first load.

### D2 — Claims are read by their OIDC names, and the display name self-heals

Three defects sit behind the `?` avatar, and each needs its own fix.

**The server reads claim names that JwtBearer has already renamed.**
`MapInboundClaims` defaults to `true`, which maps `name` onto
`ClaimTypes.Name`, so `ClaimsCurrentUser.DisplayName`'s `FindFirstValue("name")`
can never match and the method falls through to its `Subject` fallback — the
GUID. `Program.cs` sets `options.MapInboundClaims = false`. `Subject`'s
existing `?? FindFirstValue("sub")` branch then carries it, and `User.IdFor`
hashes the same string either way, so no user's identity changes.

**Already-provisioned users keep the GUID.** `ProvisionUser` returns `[]` once
`ProvisionedAt` is set — correct, and it means fixing the claim read does
nothing for an existing user. The Identity module gains `SetUserDisplayName`
and `UserDisplayNameSet`; `MeEndpoints` issues the command when the token's
name differs from the stored one. Existing users heal on next sign-in and no
migration runs.

**The client depends on a claim that may be absent.** `AppShell` reads the
`email` claim while `AddOidcAuthentication` requests only `openid profile`.
`"email"` joins `DefaultScopes`, *and* the client stops depending on it:
`MeResponse` gains `Email`, read from the claim server-side and **not
persisted** — the IdP owns a user's email, and copying it into an aggregate
buys a second copy to keep in step.

The account control shows `DisplayName` as its primary line and email as a
secondary line when there is one.

### D3 — A Settings screen, and the theme moves into it

`/settings` holds:

| Block | Contents |
|---|---|
| Account | avatar, display name, email — read-only, the IdP owns them |
| Time zone | `MudSelect` over `TimeZoneInfo.GetSystemTimeZones()`, `PUT /api/me/timezone` |
| Theme | System / Light / Dark, `localStorage`, per device |
| Sync | pending command count |

The time zone picker is the point of the screen. Keycloak sends no `zoneinfo`
claim, so `TimeZoneHint` resolves to `Etc/UTC` for everybody, and AGENTS.md §3
makes the user's zone the definition of "today". Saving refreshes
`AppState.Today` so My Day re-evaluates without a reload.

Theme leaves the account menu, amending `ui-redesign-2-design.md` D12's last
paragraph. A three-way toggle nested inside a menu item is a control that
cannot be reached by keyboard in any obvious way, and Settings is where a
person looks for it.

The time zone goes over a new `PUT /api/me/timezone`, **not** through the
offline command path, and the picker is disabled when the client is offline.
`SetUserTimeZone` lives in `PSPad.Module.Identity`, which `PSPad.App` does not
reference and should not start referencing for one control; the replica holds
no `User` document the client could decide against either. It is a rare
configuration action against server-owned state, so the one screen in the app
that needs a connection is the right trade.

**Rejected: editing the display name here.** It would need a rename command
whose value the next sign-in overwrites from the token.

### D4 — The account menu carries navigation, not settings

Rebuilt activator: avatar, display name, email, chevron. The current `?` on a
circle does not read as a control, which is the whole of defect 3.

Menu: **Settings**, **History**, **Sign out**.

### D5 — History is a sidebar row

Sidebar becomes **My Day / Inbox / Goals / History**, divider, areas,
**+ New area**.

`ui-redesign-2-design.md` D13 put History in the account menu as a
low-frequency screen. D7 below gives it the burndown chart, which makes it a
screen someone opens to look at something — the same argument ADR-0017 used
to promote Goals.

### D6 — One grid class, applied everywhere a list is drawn

```css
.pspad-grid {
    display: grid;
    gap: 16px;
    grid-template-columns: repeat(1, 1fr);
}

@media (min-width: 600px) {
    .pspad-grid { grid-template-columns: repeat(2, 1fr); }
}

@media (min-width: 960px) {
    .pspad-grid { grid-template-columns: repeat(3, 1fr); }
}

@media (min-width: 1920px) {
    .pspad-grid { grid-template-columns: repeat(4, 1fr); }
}
```

One column on a phone, two on a tablet, three inside the existing
`MaxWidth.Large` container, four once the viewport itself passes MudBlazor's
`xl` breakpoint (1920px) — wide enough that even a `MaxWidth.Large`-capped
container has room for a fourth 1fr column without any card shrinking past
its comfortable minimum. The 600px/960px thresholds match `BrowserViewport`'s
own `Breakpoint.MdAndUp` split, so the grid and the desktop/mobile shell
agree on where "wide enough" starts.

The area behind the grid carries no background of its own — each card is a
`MudPaper Outlined="true"` (border in `var(--mud-palette-lines-default)`)
with `Elevation="0"`, so separation comes from the card's own outline, not
from a coloured seam under the grid.

**Amended during implementation: fixed-width `auto-fit` tracks replaced by
breakpoint-counted columns.** A first pass used
`grid-template-columns: repeat(auto-fit, 340px)` to stop cards stretching
into a half-empty row. That backfired: three 340px tracks plus two 16px gaps
need 1052px, so an area narrower than that — common once a sidebar is
open — wrapped to two columns even where there was clearly room for a
third, narrower one. Fixing the column *count* to the breakpoint instead of
deriving it from a fixed pixel width solves both problems at once: the
column count always matches what the viewport can actually hold, and because
the column count is explicit (not `auto-fit`/`auto-fill`), a row with fewer
cards than columns leaves the remaining column empty rather than stretching
a card into it.

Applied to `AreaBoard` (list cards), `GoalsPage` (goal cards), `Today` (each
of overdue, due and completed as its own grid), `InboxPage` and `ListPage`.

Row-flow, not column-flow: DOM order stays reading order, so keyboard and
screen-reader order stay correct. A newspaper-style column flow would also
reflow every column each time one item is added.

### D7 — A screen never shows an empty state it has not verified

Every page carries `bool _loaded`, set at the end of its reload. Until then it
renders skeletons, never "Nothing due today." and never an empty container.

`Components/RowSkeleton.razor` and `Components/CardSkeleton.razor` wrap
`MudSkeleton` and render inside `.pspad-grid`, so the skeleton has the column
count the real content will have and nothing jumps when data lands. The
sidebar's area list gets skeleton rows on the same rule.

### D8 — `CreatedAt` on `TodoTask`, and the burndown is computed on the client

`TodoTask` gains `CreatedAt`, set in `When(TaskCreated)` from `created.At`.
`DomainEvent` already carries `At`, so **no event changes, nothing new goes on
the wire, and sync is untouched** — delta sync (ADR-0006) replicates the field
because it replicates the document.

Documents written before the field existed have no value. A backfill beside
`MongoIndexes.EnsureAsync` fills them from the earliest `TaskCreated` in the
event log, and skips any task that already has one.

`PSPad.Module.Tasks/Analytics/BurndownRule.cs` is the one engine, pure and
WASM-safe, sitting beside `Today/TodayRule.cs`:

- `open(d)` counts tasks where `CreatedAt <= endOfDay(d)`, `CompletedAt` is
  null or later than `endOfDay(d)`, and `Deleted` is false
- `completed(d)` counts tasks completed on `d` plus recurrence occurrences
  done on `d`
- days are bucketed in the **user's time zone**, per AGENTS.md §3, which is
  why D3's picker is a prerequisite and not a nicety
- **recurring templates are excluded from the open line.** They never close,
  so they would sit on it as a permanent flat offset that no amount of work
  reduces. Their occurrences still count in the completion bars. Same shape of
  argument as never-overdue.

**Rejected: a server `/api/analytics` endpoint.** It gives full fidelity
including deleted tasks, and it is blank offline — in an app whose every other
screen works offline. **Rejected: both.** Two engines for one number that will
disagree.

### D9 — The chart lives on History, above the log

`MudChart` — already in MudBlazor 9.9.0, so no new dependency and no licence
question, and it takes the sage palette for free. Open-count line and
completions bars, with a 30 / 90 day toggle. The event log stays below it,
unchanged.

This moves AGENTS.md §2's subsystem 8 partly into slice 1. §2 and §3 change to
say so.

## Structure

### Routes

| Route | Change |
|---|---|
| `/settings` | new |
| `/history` | gains the chart; gains a sidebar row |

### Files

**Added:** `Components/BrandLoader.razor`, `Components/RowSkeleton.razor`,
`Components/CardSkeleton.razor`, `Pages/SettingsPage.razor`,
`Module.Tasks/Analytics/BurndownRule.cs`,
`Module.Tasks/Analytics/BurndownPoint.cs`,
`Module.Tasks/Analytics/BurndownSeries.cs`,
`Module.Identity/SetDisplayName/SetUserDisplayName.cs`,
`Module.Identity/SetDisplayName/UserDisplayNameSet.cs`,
`Infrastructure/Mongo/MongoBackfill.cs`, `adr/0019-*.md`, `adr/0020-*.md`.

**Modified:** `wwwroot/index.html`, `wwwroot/css/app.css`,
`Layout/AppShell.razor`, `Layout/AccountMenu.razor`, `Layout/NavSidebar.razor`,
`Pages/Today.razor`, `Pages/InboxPage.razor`, `Pages/ListPage.razor`,
`Pages/AreaBoard.razor`, `Pages/GoalsPage.razor`, `Pages/HistoryPage.razor`,
`App/Program.cs`, `Api/Program.cs`, `Api/Identity/CurrentUser.cs`,
`Api/Endpoints/MeEndpoints.cs`, `Contracts/MeResponse.cs`,
`Module.Identity/User.cs`, `Module.Tasks/Tasks/TodoTask.cs`, `AGENTS.md`,
`adr/README.md`, `docs/src/pages/features.astro`, `docs/src/pages/index.astro`.

**Untouched:** every command, every event, every sync path, every index,
`docs/src/pages/install.astro` — nothing about what a self-hoster runs changes.

## Testing

Unit (`[UnitTest]`, xUnit v3 in-process runner — `dotnet test` discovers
nothing in this repo):

- `BurndownRule` — zone bucketing, recurring templates excluded from the open
  line, deleted tasks excluded, occurrence completions counted
- `TodoTask` sets `CreatedAt` from `TaskCreated`
- `User` handles `SetUserDisplayName`, and is a no-op when unchanged

bUnit:

- Settings sends `SetUserTimeZone` and refreshes `AppState.Today`
- the account menu offers Settings, History and Sign out, and no theme toggle
- the sidebar carries a History row
- each page renders a skeleton, and *not* its empty state, before load
- `.pspad-grid` is applied on each of the five screens

Integration (Testcontainers, `ApiFactory`):

- `/api/me` returns the token's name, not the subject
- a changed token name updates the stored display name
- the backfill fills `CreatedAt` and leaves an existing one alone

Verified by hand in a browser, as ADR-0013 established for anything the
stylesheet decides: the boot animation in both themes, and the column count at
phone, tablet and desktop width.

## Out of scope

Server-side analytics. Streaks, habits, annual plans. Per-area or per-goal
breakdowns of the chart. Editing the display name. Drag-and-drop reordering.
