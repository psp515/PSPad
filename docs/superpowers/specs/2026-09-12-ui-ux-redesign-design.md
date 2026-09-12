# UI and UX Redesign — Design

**Status:** approved in brainstorming, 2026-09-12
**Supersedes:** the placeholder UI delivered by plan 07

## Problem

Plan 07 delivered screens that work and are covered by bUnit tests, but the
UI is skeletal. Every screen is a heading, a permanently visible text field
and a flat `MudList`. There is no theme, no responsive behaviour, no way to
reach a list in fewer than three taps, and the drawer is hardcoded open at
every width. Two concrete defects illustrate the state of it:

- `Today.razor` marks overdue rows with the CSS class `overdue`, which
  `app.css` never defines. Overdue tasks — the thing the Today screen exists
  to surface — render identically to everything else.
- Inbox "organise" sends every item to `lists.FirstOrDefault()`. There is no
  picker, so organising is effectively broken, despite AGENTS.md §3 calling
  it a first-class command.

`app.css` is still the stock Blazor template's Bootstrap leftovers.

## Goals

Make the client usable on phone, tablet and desktop; give it light and dark
themes; make creating things a deliberate, discoverable act rather than a row
of always-open text fields; and make lists read the way a task app should.

Out of scope: any change to commands, events, aggregates or sync. This is a
presentation-layer redesign. `PSPad.Module.Tasks` is not touched, so AD-3 and
AD-4 are unaffected.

## Decisions

### D1 — Sections and areas live on separate surfaces, at every width

App sections (Today, Inbox, Areas, Goals, History) and user areas (Personal,
Work, …) never share one navigation surface. Areas are unbounded user data;
sections are a fixed set of five. Mixing them forces the count of areas to
stay small, which nothing guarantees.

| Width | Sections | Areas |
|---|---|---|
| `xs`/`sm` (<960px) | bottom bar: Today, Inbox, Areas | bottom sheet, opened from the Areas item |
| `md`+ (≥960px) | persistent left sidebar | horizontal chip row in the content pane |

Goals and History sit in the sidebar at `md`+, and in the area sheet's footer
below that — they are low-frequency screens and do not earn a bottom-bar slot.

**Rejected:** areas as the bottom-bar tabs, as originally sketched. It pushes
Today and Inbox — the two hot paths AGENTS.md §1 names as the product's
purpose — off the primary surface, and breaks down past about four areas.

### D2 — No hamburger at any width

At `md`+ the sidebar is permanent, so there is nothing to toggle. Below that
the bottom bar replaces it entirely. The drawer toggle and its icon button
are deleted rather than hidden.

### D3 — Breakpoint switching is CSS-driven

Structural switches use MudBlazor's responsive display utilities —
`d-none d-md-flex` and `d-md-none` — applied as `Class` on the nav
containers. `md` begins at 960px, matching the table in D1.

**Not `MudHidden`.** Despite the name suggesting a CSS wrapper, `MudHidden`
resolves through `IBreakpointService`, which listens to JS resize events. It
therefore renders its default branch before the first breakpoint callback
arrives, flashing the wrong navigation on load. The display utilities are
resolved by the stylesheet before first paint and cost no interop.

Both branches exist in the DOM at all times and are hidden by CSS. That is
what makes the switch flash-free, and it is also why breakpoint behaviour is
verified by hand in a browser rather than asserted in bUnit, which applies no
stylesheet.

### D4 — Area chips scroll; they never wrap

A wrapping chip row pushes content down by an unpredictable amount as areas
accumulate. The row scrolls horizontally instead. No overflow menu is needed
because the sidebar's Areas entry remains the complete, scrollable list.

### D5 — Today is never filtered by area

Today shows no chips. AGENTS.md §3 is explicit that Today answers "what do I
do now" across all areas; an area filter there would undercut the one screen
the product is built around.

### D6 — The FAB is a split control: tap acts, caret expands

A single tap performs the obvious action for the current screen. A visible
caret opens a menu of the rarer cross-screen actions.

| Screen | Tap | Caret menu |
|---|---|---|
| Today | quick-capture to Inbox | new task, new list, new area |
| Inbox | focus the capture field | new task, new list, new area |
| List | new task in this list | capture, new list, new area |
| Areas | new area dialog | capture, new task |
| Goals | new goal dialog | capture |
| Task detail | add step | capture |

Today's tap capturing to Inbox rather than creating a task is deliberate: a
task needs a list, and stopping to choose one is exactly the friction GTD
capture exists to avoid.

**A visible caret, not long-press.** Long-press has no good pointer
equivalent, and this UI has to work with a mouse.

Pages declare their actions through a scoped `FabContext` service rather than
the layout inspecting routes. Route inspection couples the layout to the
router and cannot be unit-tested without rendering.

### D7 — Theme preference is per-device and local

Three modes: System (default), Light, Dark. The preference persists in
`localStorage`, not on the `User` aggregate.

This is a deliberate split from time zone, which plan 05 put on `User`
server-side. Time zone is a property of the person and must agree across
devices, because the Today rule depends on it. Theme is a property of the
device and the light it sits in — the same person plausibly wants dark on a
phone at night and light on a desktop by a window. Keeping it local also
means no new command, no event, and no change to any aggregate.

### D8 — Creation is deliberate, not ambient

The always-visible "New area" / "New list" / "New task" text fields are
removed. Creating an area or a goal opens a dialog. Creating a task or a list
happens through the FAB. The inbox keeps an inline capture field, because
capture is the one action that must stay one keystroke from empty.

### D9 — Organising an inbox item expands it in place

Tapping an item expands it within the list: rename, choose target area and
list, confirm. No modal. Processing ten captured items is ten quick
interactions rather than ten modal open/close cycles, and the inbox stays a
simple list as it reads down the page.

### D10 — One task row component, shared

Today and the list page render the same `TaskRow`, modelled on Microsoft To
Do: checkbox, name, and a metadata line carrying only what applies — list
name (on Today only, since a list page already names it), due date, step
progress, a star, a priority dot, a recurrence glyph. Overdue styling comes
from the theme's error colour, not a hand-rolled CSS class.

A single component means the never-overdue rule for recurring tasks cannot
drift between the two screens that display it.

## Structure

The redesign lands as two plans.

**Plan 09 — shell.** Theme and palettes, the responsive navigation shell,
the FAB contract, the `app.css` purge. Delivers the frame every screen hangs
in, plus one screen (Today's capture) wired to the FAB to prove the contract
end to end.

**Plan 10 — screens.** `TaskRow`, then Today, the list page, Inbox with
inline organise, Areas with its dialog, Goals, History and task detail
redrawn against the shell.

Shell first, because the theme and the FAB contract are cross-cutting: every
screen consumes both, and settling them once means the screens get written
once rather than rewritten as each new requirement surfaces.

## Testing

bUnit component tests, `[UnitTest]`, following the conventions plan 07
established: `Render<T>()` rather than the obsolete `RenderComponent<T>()`,
and `JSInterop.Mode = JSRuntimeMode.Loose` because MudBlazor's inputs call
into `mudKeyInterceptor`.

What is worth testing here is behaviour, not appearance: that the sidebar
lists every section, that the area sheet lists every area and raises a
selection, that the FAB invokes the primary action on tap and reveals
secondary actions on the caret, that the theme service resolves and persists
the right mode. Palette values and spacing are not asserted — they are
judgement, and a test that pins them only makes changing them expensive.

Breakpoint switching is verified by hand in a browser, for the reason given
in D3: both branches are always in the DOM and only CSS separates them, and
bUnit applies no stylesheet.
