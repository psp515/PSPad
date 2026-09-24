---
title: One FAB per page's action set — zero, a plain FAB, or a FAB Menu
tags: [ui]
date: 2026-09-24
status: Active (amends [0022](0022-drawer-cleanup-settings-and-app-info-in-sidebar-area-fab.md))
---

# ADR-0033: One FAB per page's action set — zero, a plain FAB, or a FAB Menu

## Context

ADR-0022 gave `AreaBoard.razor` a `MudFab` for "New list" and, as a
deliberate, narrow reversal of `ui-redesign-2-design.md` D6/D7, a second
`MudFab` in the opposite corner opening a Rename/Delete menu — scoped to
areas only, with lists keeping their existing `⋯` `ThingMenu` on the card
header and every other page keeping its inline creation field. That ADR's
own Consequences section flagged the result as inconsistent on its face:
"a future reader of `AreaBoard.razor` finds one item that doesn't follow
D6/D7's logic."

Issue #26 set out to fix that one page — two FABs competing for the same
bottom-right corner, only one of which was reachable without the other
covering it on narrow screens. Looking at the area page next to the rest of
slice 1 exposed the wider problem ADR-0022 had already named: GoalsPage
created via an inline text field with no rename/delete affordance at all,
ListPage created via an inline field plus a separate header `⋯` menu for
rename/delete, AreaBoard had two FABs, and InboxPage already used a single
FAB opening a capture dialog — four pages, four different answers, with no
rule saying which pattern a fifth page should copy. Brainstorming this with
the maintainer over issue #26 turned a single-page bug fix into a
project-wide rule: every page's own page-level action set — the actions
that create, rename or delete the *page's own subject* (an area, a list, a
goal), as distinct from the item-level actions already living on each
card or row — should drive one predictable affordance, not a bespoke
layout per page.

## Decision

Every page's page-level action set (create/rename/delete of the page's own
subject) drives exactly one bottom-right affordance, chosen by how many
actions there are:

- **Zero actions** → no FAB at all.
- **One action** → a plain `MudFab`.
- **Two or more actions** → one `MudFab` activator opening a `MudMenu`
  ("FAB Menu", the shared `PageFabMenu.razor` component), whose items are
  icon-only `MudMenuItem`s wrapped in a `MudTooltip` carrying the visible
  label.

This supersedes ADR-0022's "opposite corner, two FABs" arrangement for
areas specifically: `AreaBoard.razor` now shows one `PageFabMenu` with New
list / Rename area / Delete area. The same shape now applies uniformly to
`GoalsPage.razor` (one action — Achieve/Reopen/Delete stay per-card, so the
page-level action is just "new goal" — a plain `MudFab`) and
`ListPage.razor` (three actions — Add task / Rename list / Delete list — a
`PageFabMenu`, replacing its old inline add-task field and header `⋯`
menu). Item-level actions (rename/delete a single list's card on
`AreaBoard`, a single goal's card on `GoalsPage`) are unaffected and keep
using `ThingMenu` on the card/row itself — this ADR governs only the
page's own subject.

## Considered alternatives

- **Keep the two-FAB layout on `AreaBoard`, just drop the icon-only
  requirement (let each FAB carry a label or a distinct color).** Rejected
  — it papers over the corner-collision problem on narrow screens without
  fixing the actual inconsistency the maintainer flagged: other pages
  still would have had no equivalent rule, and a sixth page would have
  faced the same "what do I do here" question again.
- **Text-labeled menu items instead of icon-only.** Rejected per
  `specs/ui-spec.md`'s (formerly `fab-pattern-design.md`'s) explicit
  stance that a visible text label on a FAB Menu item is a last resort,
  not a default — icon plus tooltip keeps the menu compact and consistent
  with Material FAB conventions, and a tooltip is enough for a sighted,
  pointer-driven user to identify an unfamiliar icon.

## Consequences

Every page in the app now answers "how do I create/rename/delete the
thing this page is about" the same way — a developer building a sixth page
doesn't invent a new layout, they count the page-level actions and pick
zero/one/many. `AreaBoard.razor` loses the "point of divergence" ADR-0022's
own Consequences called out; that paragraph is now stale and superseded by
this decision.

The cost, found during this same fix wave: icon-only `MudMenuItem`s wrapped
only in `MudTooltip` have no accessible name for assistive technology or
touch input — `MudTooltip` is hover/focus-driven and does nothing on a
touchscreen, which this project's "works on phone" vision treats as a hard
requirement. That gap is closed in the same push that wrote this ADR, by
adding `aria-label` (via each component's `UserAttributes`) to every FAB
Menu item and to the `PageFabMenu` activator itself — but it is worth
recording here as an honest consequence of standardizing on an icon-only
FAB Menu: every future FAB Menu item this pattern produces needs the same
`aria-label` discipline, and nothing in the pattern enforces it structurally
yet.
