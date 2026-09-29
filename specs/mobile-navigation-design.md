# Mobile navigation — design

Status: Approved, not built. Decision of record: ADR-0050 (written with the
implementation).

Mockups: https://claude.ai/artifact/RMCE7jAdQwnz1mQfoNMN9y (My Day, Areas tab,
nested list, account drawer, bottom bar).

Below `md` (960px) the app drops the navigation drawer for a top bar, a bottom
navigation bar and a right-hand account drawer. From `md` up nothing changes:
the permanent `NavSidebar` stays exactly as it is.

## 1. Scope

In:

- A top bar on phones: back arrow (nested screens only), screen title,
  `ConnectionStatus`, avatar.
- A bottom bar on phones: Inbox, Areas, My Day, Goals, Statistics. My Day is
  the raised centre button.
- A right-hand account drawer on phones, opened from the avatar.
- Area chips at the top of the area screen on phones.
- A `/areas` route that opens the area last used on this device.
- Tonal page FAB, hidden scrollbars, extra bottom padding — phones only.

Out:

- Any change at `md`+.
- Search (still unreachable, `adr/0023`).
- Swipe gestures between tabs.
- Changes to detail panels: they stay full screen on phones and cover both
  bars.

## 2. Shell below `md`

Removed from `AppShell`: the temporary `MudDrawer`, the hamburger
(`pspad-drawer-toggle`) and `_drawerOpen`. `NavSidebar` loses its `Navigated`
parameter, which only closed that drawer.

Added to `AppShell`, each with `d-md-none` (display utilities, never
`MudHidden`, as ui-spec §2 already requires):

- `MobileTopBar`
- `BottomNav`
- `AccountDrawer`

### 2.1 Top bar — `Layout/MobileTopBar.razor`

The existing dense `MudAppBar`, left to right:

1. A back `MudIconButton` (`ArrowBack`) when `PageHeader.BackHref` is set.
2. `PageHeader.Title`, `Typo.h6`, `Color.Primary`, truncated with an
   ellipsis. It may carry a caption subtitle (a list screen shows its
   area's name).
3. `ConnectionStatus`.
4. A `MudAvatar` button (`AvatarColor`, initials) that opens
   `AccountDrawer`.

### 2.2 Page titles — `State/PageHeader.cs`, `Components/PageHeading.razor`

`PageHeader` is a scoped service: `Title`, `Subtitle`, `BackHref`, and a
`Changed` event that `MobileTopBar` listens to.

`PageHeading` replaces the hand-written title and back arrow on every page
(ui-spec §3 title pattern):

- It sets `PageHeader` from its parameters on every parameter set.
- It renders today's desktop title markup inside `d-none d-md-flex`.

One component per page means the phone and desktop titles cannot drift.
Every routed page inside `AppShell` renders a `PageHeading`, NotFound
included, and a page's loading branch renders one with an empty title (no
desktop markup), so the bar never keeps the previous screen's title.
Clearing from `AppShell` on location change is ruled out: a panel's query
change fires it without re-rendering the page, which would blank the title.

Extras under a title (the goal screen's status chip and due date) stay in
the page, on their own row below `PageHeading`, at every width. The goal
screen also passes its status text as the subtitle.

### 2.3 Bottom bar — `Layout/BottomNav.razor`

`MudAppBar Bottom="true"`, five equal slots in order:

| Slot | Target | Extra |
|---|---|---|
| Inbox | `/inbox` | `MudBadge` with `SidebarCounts.Inbox`, hidden at 0 |
| Areas | `/areas` | |
| My Day | `/` | raised `MudFab`, `Color.Primary`, always filled |
| Goals | `/goals` | |
| Statistics | `/statistics` | |

The four side slots are `MudButton`s: icon over label, the same Material icons
as `NavSidebar`. The active slot gets a `Color.Primary` icon, a bold label and
a tonal pill behind the icon. The active My Day button gets a tonal ring
instead.

The active slot comes from `State/NavTab.cs`, a pure function of the path:

| Path | Tab |
|---|---|
| `/` | My Day |
| `/inbox` | Inbox |
| `/areas`, `/areas/{id}`, `/lists/{id}` | Areas |
| `/goals`, `/goals/{id}` | Goals |
| `/statistics` | Statistics |
| anything else (`/settings`, `/app-info`, `/search`) | none |

Query strings (detail panels) never change the tab.

### 2.4 Account drawer — `Layout/AccountDrawer.razor`

`MudDrawer`, `Anchor.End`, `DrawerVariant.Temporary`, 300px wide. Top to
bottom:

1. `AccountBadge`
2. Divider
3. Settings and App info as `MudNavLink`s
4. Spacer
5. `SidebarFooter`

Navigating closes it.

`Components/SidebarFooter.razor` is the footer lifted out of `NavSidebar`
(connection status, clock, "PSPad · GPL v3") and used by both.

### 2.5 Areas tab — `Pages/AreasIndex.razor`, `State/LastArea.cs`

`LastArea` stores the id of the last area opened on this device in local
storage, the same way `CardCollapseState` does. `AreaBoard` writes it when it
loads.

`/areas` resolves, in order:

1. the remembered area, if it still exists and is not deleted;
2. otherwise the first area by `Position`;
3. otherwise an `EmptyState` with a **New area** action (`?area=new`).

Cases 1 and 2 redirect with `replace: true`, so Back never lands on `/areas`.

### 2.6 Area chips — `Components/AreaChips.razor`

This sits at the top of `AreaBoard` inside `d-md-none`, so desktop is
unchanged. It is a wrapping row of small `MudChip`s inside a
`navigation` landmark:

- one chip per non-deleted area in `Position` order, each a link
  (`Href="/areas/{id}"`); the current one is filled `Color.Primary` with
  `aria-current="page"`, the rest outlined;
- a trailing **New area** chip (`Icons.Material.Filled.Add`, outlined,
  `Color.Primary`) that opens `?area=new`.

Chips are links rather than a `MudChipSet` selection: choosing an area is
navigation, and a chip set would also make **New area** selectable.

### 2.7 FAB, padding, scrollbars

Below `md`:

- **FAB.** Each page's FAB behaves as before (`adr/0033`). It sits above the
  bottom bar and is tonal: the accent's light shade behind a dark icon. The
  raised My Day button is the only solid accent circle.
- **Padding.** `pspad-content`'s bottom padding is the FAB inset plus the
  bottom bar's height, so neither covers the last row.
- **Scrollbars.** Scrolling containers hide their scrollbars
  (`scrollbar-width: none`, `::-webkit-scrollbar { display: none }`) and still
  scroll.

At `md`+ all three stay as they are today.

## 3. Testing

All `[UnitTest]`, written first.

- **`NavTab`:** every path in §2.3's table, plus a query string on each.
- **`BottomNav`:**
  - The active slot follows the current URI.
  - The Inbox badge shows when the count is above zero and hides at zero.
  - My Day links to `/`.
- **`PageHeading`:**
  - Sets `PageHeader` title, subtitle and back link.
  - Renders the desktop title inside `d-none d-md-flex`.
- **`MobileTopBar`:**
  - Shows the back arrow only when `BackHref` is set.
  - Redraws on `PageHeader.Changed`.
  - The avatar opens `AccountDrawer`.
- **`AreaChips`:**
  - The current area's chip is selected.
  - Tapping a chip navigates to `/areas/{id}`.
  - **New area** adds `?area=new`.
- **`AreasIndex`:**
  - Opens the remembered area.
  - Falls back to the first area when the remembered one is deleted.
  - Shows the empty state when there are no areas.
- **`AppShell`:**
  - No temporary drawer and no `pspad-drawer-toggle`.
  - Navigating closes the account drawer.
  - Existing tests that use the toggle are updated.

## 4. Documentation shipped with the change

- `adr/0050` records bottom navigation below `md`. It amends `adr/0022` and
  `adr/0023` for phones only, and adds a row to `adr/README.md`.
- `specs/ui-spec.md`:
  - §2 "Sidebar breakpoint" and "Mobile app bar" rewritten.
  - §2 container padding updated.
  - §3 title pattern points at `PageHeading`.
  - §5 lists `/areas` and the phone navigation.
- `docs/src/pages/features.astro`: one line on the phone layout.
