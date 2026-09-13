---
title: Keep sections and areas on separate navigation surfaces, and the theme per device
tags: [ui, offline, identity]
date: 2026-09-12
status: Superseded by [0014](0014-one-navigation-tree-at-every-width.md)
---

# ADR-0013: Responsive navigation shell and per-device theme

## Context

The client shipped by plan 07 had one drawer, hardcoded open at every width,
and no theme. Making it usable on a phone forced two questions that the screen
work could not answer on its own.

The first is what navigation carries. The app has five fixed sections — Today,
Inbox, Areas, Goals, History — and an unbounded number of user areas. A phone
offers room for three or four primary destinations. Any design that puts areas
on that surface implicitly caps how many areas a user may usefully have, and
does so silently: nothing in the domain limits them.

The second is where a theme preference lives. Plan 05 established that the
user's time zone belongs on the `User` aggregate, server-side, because the
Today rule depends on it. That precedent invites putting theme there too.

## Decision

We will keep app sections and user areas on separate navigation surfaces at
every width: a persistent sidebar plus an area chip row at `md` and above, a
bottom bar plus an area sheet below it. Neither surface ever carries both.

We will store the theme preference in the browser's `localStorage`, not on the
`User` aggregate.

## Considered alternatives

- **Areas as the bottom-bar tabs.** Rejected. It displaces Today and Inbox,
  which AGENTS.md §1 names as the product's purpose — one screen answering
  "what do I do now", and frictionless capture. It also degrades past about
  four areas, and the degradation is invisible until a user has five.
- **A single merged sidebar listing sections and areas together**, as
  Microsoft To Do does. Rejected for this app because the mobile layout cannot
  mirror it, so the two widths would have diverged into genuinely different
  information architectures rather than one model rendered two ways.
- **`MudHidden` for the breakpoint switch.** Rejected on inspection: despite
  the name it resolves through `IBreakpointService`, which listens to JS
  resize events, so it renders its default branch before the first callback
  and flashes the wrong navigation on load. MudBlazor's CSS display utilities
  resolve before first paint instead.
- **Theme on the `User` aggregate, beside the time zone.** Rejected. A time
  zone is a property of the person and must agree across their devices or the
  Today rule is wrong on one of them. A theme is a property of the device and
  the light around it: the same person reasonably wants dark on a phone at
  night and light on a desktop by a window. Putting it on `User` would also
  mean a new command, a new event and an aggregate change to record a
  preference no other device should inherit.

## Consequences

Two navigation trees exist and are maintained separately; a new section must
be added to both. Both render into the DOM at all times and are separated only
by CSS, which is what makes the switch flash-free but also means bUnit cannot
assert which one a user sees — breakpoint behaviour is verified by hand.

The area chip row scrolls horizontally rather than wrapping, so a user with
many areas scrolls; the sidebar's Areas entry remains the complete list, so
nothing becomes unreachable.

Theme does not follow a user to a new device, and clearing site data resets it
to System. That is the accepted cost of not modelling it as user state. If it
later turns out people expect theme to travel with the account, this decision
has to be superseded rather than amended, and the preference moved onto `User`
with a command and an event like any other user state.

No command, event or aggregate changed to deliver this, so AD-3 and AD-4 are
untouched: `PSPad.Module.Tasks` still compiles to WebAssembly with no
infrastructure reference, and the shared-contract story is unaffected.
