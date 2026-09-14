# SDD ledger — plan: .superpowers/sdd/ui-redesign-2/14-thing-menus.md

Done on `feature/psp/ui-redesign-2` after 12, 13, 15 (Task 5) and 16 had already
landed on the branch, since the maintainer asked for everything on one branch
for a single PR.

## Task ordering deviation

Plan's own Task 2 wires `OnRenameArea`/`OnDeleteArea` onto `NavSidebar` before
Task 3 adds those parameters to `NavSidebar` — that would not compile at
Task 2's own checkpoint. Did Tasks 2 and 3 together as one commit instead of
splitting them; noted in that commit's message rather than silently reordering
without saying so.

## MudBlazor version corrections (recur through every task)

The plan's test snippets don't match the installed MudBlazor version in two
ways, discovered by dumping actual rendered markup rather than guessing:

- Menu items render with class `.mud-menu-item`, not `.mud-list-item` as every
  plan test snippet assumed.
- `MudMenu`'s open content (and `IDialogService`'s dialogs) portal through
  `MudPopoverProvider`, not inline in the component's own render tree —
  confirmed by an existing precedent in `AccountMenuTests.cs` that the plan
  did not point at. Every test that opens a `ThingMenu` or a dialog wraps a
  `RenderFragment` combining `MudPopoverProvider` (+ `MudDialogProvider` for
  dialog tests) with the component under test, mirroring that established
  pattern. `ThingMenu`'s own activator (Icon-mode `MudMenu`, unlike
  `AccountMenu`'s custom `ActivatorContent`) does carry a real click handler,
  so `.Click()` on the button works once the popover has somewhere to render.

The plan's Task 5 note ("copy the arrangement from `TaskDetailPanelTests`,
which already shows dialogs") is wrong — that file renders no dialog at all.
Built the `MudPopoverProvider` + `MudDialogProvider` + page-under-test
RenderFragment pattern directly for `AreaBoardTests` and reused it for
`ListPageTests`.

`ClickingNewAreaRaisesOnNewArea` (pre-existing) used a blind `Find("button")`
that would resolve to the first area row's new `ThingMenu` button instead of
"New area" once rows carry menus. Gave the button a `.pspad-new-area` class
and scoped both the test and the existing `Arrange` button-click helper.

## Verification

148/148 `PSPad.App.Tests`, 81+6+6 module tests, full solution build clean
after every task.

Also verified live in the browser (docker compose stack rebuilt with this
branch's `app` image, Keycloak `demo`/`demo`): created two areas via `+ New
area`, renamed one via its sidebar row menu, added a list, renamed it from
the card menu, moved it to the other area via the picker dialog (confirmed
the picker excludes the area the list started in), confirmed the list left
the source area and landed in the target, deleted it from its own screen
title menu (redirected back to its area), then deleted both scratch areas to
clean up. Every step matched what the unit tests already asserted internally.

One side note, not a defect in this track: browser-driven area/list actions
apply optimistically to the client's IndexedDB replica and only reach Mongo
once `SyncCoordinator` flushes the outbox: closing the browser right after an
action (as an early pass at this check did) can lose an unsynced command from
that ephemeral test profile before it reaches the server. That's the offline
architecture (AD-5/AD-6) working as designed, not something plan 14 touches —
noted here only because it cost a round of confused re-checking before the
cause was clear.

Plan 14 COMPLETE. Six commits on `feature/psp/ui-redesign-2`:
`feat(app): a shared thing menu and a confirm dialog`,
`fix(app): the new area button creates an area, rename and delete from the sidebar row`,
`feat(app): a menu on the list card header`,
`feat(app): rename, delete and move a list from its card`,
`feat(app): rename and delete a list from its screen`.
