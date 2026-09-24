# FAB / FAB Menu convention (issue #26)

Date: 2026-09-24
Status: approved, not yet implemented

## Context

`specs/ui-spec.md` §3 currently forbids page-level FABs outright except for
one exception on the area page (rename/delete area). Issue #26 asks for that
exception itself to be cleaned up — AreaBoard shows two separate FABs (a
`+ New list` FAB and a `MoreHoriz` FAB opening a Rename/Delete menu) where it
should show one. Fixing that single page exposed a broader inconsistency:
GoalsPage creates via an inline text field, ListPage creates via an inline
field plus a header `⋯` menu for rename/delete, while InboxPage already uses
a single FAB opening a capture dialog. There was no single rule governing
when a page gets a FAB, so each page grew its own answer. This spec settles
one rule and reworks every page to follow it, and rewrites the relevant
section of `specs/ui-spec.md` to match.

## The rule

Every page has a *page-level action set*: actions that belong to the page's
own subject — creating the thing the page is about, renaming or deleting
that thing. This is distinct from *item-level* actions that already live on
each card or row inside the page (a list card's `ThingMenu`, a `GoalCard`'s
achieve/reopen, an inbox item's open) — those are unaffected by this rule.

- **Zero page-level actions** → no FAB.
- **One page-level action** → a single plain `MudFab`, bottom-right,
  performs the action directly (typically opening a dialog).
- **Two or more page-level actions** → a single `MudFab`, bottom-right,
  opens a `MudMenu` (the "FAB Menu") listing every page-level action. Never
  a second FAB, never a header `⋯` menu competing with it.

**FAB Menu items are icon-only.** A `MudMenuItem` in a FAB Menu shows an
icon with no visible text and carries a `MudTooltip` for the accessible
label. A text label is a last resort, only when an action has no icon that
reads unambiguously on its own.

## Per-page application

| Page | Page-level actions | Result | Removes |
|---|---|---|---|
| AreaBoard | New list, Rename area, Delete area | one FAB Menu | today's two-FAB layout |
| GoalsPage | Add goal | one plain FAB → `NameDialog` | the inline "New goal" field |
| ListPage | Add task, Rename list, Delete list | one FAB Menu | the inline "Add task" field and the header `⋯` (`ThingMenu`) |
| InboxPage | Capture | one plain FAB (unchanged) | — |
| Today (My Day) | none | no FAB (unchanged) | — |
| Settings | none | no FAB (unchanged) | — |
| History | none | no FAB (unchanged) | — |

Item-level affordances stay exactly as they are today: `ListCard`'s
per-card `+` and `ThingMenu`, `GoalCard`'s achieve/reopen/rename/delete,
`InboxItemCard`'s open. Those follow the existing "rename/delete on the
thing" rule, which this spec does not change.

## Implementation shape

- Extract the FAB-Menu markup (currently hand-rolled once, on AreaBoard) into
  a shared component, `Components/PageFabMenu.razor`: a `MudFab` activator
  plus a `MudMenu`, taking a list of `(icon, tooltip, onClick)` entries as
  parameters or child content. AreaBoard and ListPage both consume it instead
  of duplicating `MudMenu` + `MudFab` markup.
- Plain-FAB pages (GoalsPage, InboxPage — already correct) keep a bare
  `MudFab`, no shared component needed.
- Dialogs: reuse `NameDialog` for "New list" / "New goal" (already the
  pattern for AreaBoard's list creation and area/list/goal renames). Reuse
  the existing `AddTaskDialog` for ListPage's "Add task" FAB item — today
  it's only reachable through `ListCard`'s per-card `+`, this adds a second
  caller from the page-level FAB Menu.

## Testing

bUnit component tests per changed page:

- FAB renders where the rule says it should (and doesn't where it says it
  shouldn't).
- Clicking a FAB Menu's activator opens the menu; each item invokes the
  right handler with the right dialog.
- The inline field / header `⋯` menu removed from that page no longer
  renders.
- FAB Menu items expose an icon and a tooltip, not visible text (except the
  documented last-resort case, if any page ends up needing it).

## Spec updates

Rewrite `specs/ui-spec.md` §3: replace the "Creation is offered where the
thing is created" table and the "Empty top-level FAB budget" paragraph with
the rule and per-page table above. Drop the area-page-only exception
language — it's superseded, the rule is now general.
