# Visual refresh — lighter cards, rows and headings — design

Status: Designed. Follows #112 (PR #116, same branch). Mockup: canvas
[PSPad My Day](https://claude.ai/artifact/Q4CSixpaBmT26cS4M6dFfa), artboards
"Area board — current" vs "Area board — refreshed".

Task cards and list cards read heavy: hard grey outlines, a divider under every
header and between rows, green square checkboxes, green titles, bright stars on
every row. The refresh keeps every layout and behaviour and changes the visual
language once, through theme tokens and shared components, so every page moves
together. Temporary file: the last plan task folds it into `ui-spec.md` and
deletes it.

## 1. Scope

In: theme tokens; `MudPaper` outlined surfaces, expansion panels, buttons, chips,
inputs; `TaskRow`; `ThingCard`; a new `SectionHeading`; `PageHeading` title and
subtitle; `AreaChips` and toggle groups; `PanelSection` and panel rows;
`EmptyState`; `ui-spec.md` §1–§4.

Out: layouts, navigation, routes, accent presets, typography scale (except
section headings and the page title weight), new pages, behaviour other than the
card collapse control (§3.3).

## 2. Tokens

- `LayoutProperties.DefaultBorderRadius = "12px"`.
- CSS custom properties on `:root`, set for light and dark (dark under
  MudBlazor's dark class — follow how `app.css` already switches palettes):
  - `--pspad-line`: light `rgba(0,0,0,.08)`, dark `rgba(255,255,255,.07)` —
    card and panel edges, dividers.
  - `--pspad-hover`: light `rgba(0,0,0,.04)`, dark `rgba(255,255,255,.04)` —
    row hover.
  - `--pspad-tint`: `color-mix(in srgb, var(--mud-palette-primary) 12%, transparent)`
    — selected/active fills (same value as the sidebar's active link).
  - `--pspad-raised`: light `#FFFFFF`, dark `#1B1B1B` — card surface.
  - `--pspad-card-shadow`: light `0 1px 2px rgba(0,0,0,.06)`, dark
    `0 1px 2px rgba(0,0,0,.3)`.
- Palette: `LinesDefault`, `TableLines` and the divider colour stay as they are
  (inputs and real UI boundaries keep their 3:1 line); the soft `--pspad-line`
  is only for decorative surface edges and dividers (§2.1).

### 2.1 Contrast

`ui-spec.md` §4's 3:1 rule for lines applies to boundaries a control needs to
be identified (inputs, checkboxes, toggles). Card edges and dividers become
decorative: the card is told apart by its surface and spacing, so `--pspad-line`
is exempt. Text and icons keep 4.5:1 / 3:1:
- selected chip / toggle text = `TextPrimary` on `--pspad-tint` (≥ 4.5:1, same
  test as the sidebar link);
- round checkbox outline = `ActionDefault` on `--pspad-raised` (≥ 3:1);
- unstarred star = `TextSecondary` (≥ 3:1 on `--pspad-raised`);
- muted meta text = `TextSecondary` (≥ 4.5:1 on `--pspad-raised`).
Every pair is asserted for every preset and the awkward custom accents, light
and dark, in `PSPadThemeTests`.

## 3. Components

### 3.1 Surfaces

- `.mud-paper-outlined` (every `MudPaper Outlined="true"`): background
  `--pspad-raised`, border `1px solid var(--pspad-line)`, radius 12px,
  `box-shadow: var(--pspad-card-shadow)`. Clickable cards (`.pspad-day-task`,
  `.pspad-task-card`, goal and inbox cards) get a hover border one step stronger
  (`color-mix` of `--pspad-line` toward `TextSecondary`, or 14% alpha).
- `MudExpansionPanels` (Completed, Statistics feed): one rounded card, soft
  border, no hard top/bottom rules.
- `MudDivider` inside cards and panels: `--pspad-line`.
- Inputs (`.mud-input-outlined`): radius 10px.
- Buttons (`MudButton` filled/outlined/text) and `MudChip`: pill radius
  (`999px`). Icon buttons unchanged.

### 3.2 Task row (`TaskRow`)

- No row dividers; rows are separated by spacing; a row inside a card has
  radius 8px and `--pspad-hover` on hover.
- Checkbox: round — `UncheckedIcon = Icons.Material.Outlined.Circle`
  (`RadioButtonUnchecked`), `CheckedIcon = Icons.Material.Filled.CheckCircle`,
  unchecked colour `ActionDefault`, checked colour primary.
- Name: `TextPrimary`, 14px. Meta line: 11.5–12px, `TextSecondary`; the time
  keeps primary and medium weight; overdue date keeps error colour.
- Star: unstarred `TextSecondary`, starred primary filled.
- Applies wherever `TaskRow` renders (My Day, list cards, list page, goals,
  search); `ReferenceRow` gets the same row, star and meta treatment.

### 3.3 List card (`ThingCard`)

- Header: the list icon sits in a 30px tile (radius 9px, `--pspad-tint`
  background, primary icon). **The tile is the collapse toggle** — a
  `MudIconButton`-equivalent button with `aria-expanded` and
  `aria-label="Collapse {name}"` / `"Expand {name}"`; the chevron button is
  removed. Collapse state and persistence (`CardCollapseState`) are unchanged.
- Name: `TextPrimary`, 14.5px, medium; still the link to the list.
- Count: a muted pill (`--pspad-hover` background, `TextSecondary`, 11px) with
  the open count number only (`7`), `aria-label="7 open"`.
- Actions (+, ⋯): `TextSecondary` icons, 28px, radius 8px.
- No `MudDivider` under the header or above "Show all"; "Show all N" is a text
  link (primary, 12.5px).

### 3.4 Headings

- New `Components/SectionHeading.razor`: `Title`, optional `Count` (int?),
  optional `Color` (default `TextPrimary`; Overdue passes error, Schedule
  primary), optional `Hint` (muted text after the title, e.g. Starred's
  "· when you have time"). Renders an `h2` with class `pspad-section-heading`:
  13px, weight 600, letter-spacing .2px, margin-bottom 8px; the count as a muted
  pill after the title.
- Replaces the section headings on My Day (Overdue, Schedule, any time, Starred,
  Coming up, past-day Completed), Inbox, List page (open / completed), Goals,
  Goal, Statistics, Settings — every `MudText Typo.subtitle2` used as a section
  title. Existing section CSS classes stay on their `section` elements.
- `PageHeading` (desktop): title `TextPrimary`, h5, weight 500 (was primary
  colour); `Subtitle`, when given, shows muted after the title on the same
  baseline. Area board passes `"{lists} lists · {open} open"`.
- `PanelSection` title: the same caption style as `SectionHeading` (not
  overline/uppercase); the divider above it uses `--pspad-line`.

### 3.5 Chips and toggles

- `AreaChips`: all chips pill, no outline; current chip `--pspad-tint` with
  `TextPrimary` and medium weight; others `--pspad-hover` with `TextSecondary`;
  "New area" text-style primary.
- `MudToggleGroup` (One-time / Repeating, priorities): pill, selected item
  `--pspad-tint` + `TextPrimary`.

### 3.6 Panel rows and empty states

- `PropertyRow` activator: radius 8px, `--pspad-hover` on hover.
- `EmptyState`: icon inside a 48px circle with `--pspad-tint`; dashed border
  uses `--pspad-line` doubled (2× alpha) so it still reads as a target.

## 4. Testing

- `PSPadThemeTests`: radius 12px; each §2.1 pair for every palette.
- bUnit: `SectionHeadingTests` (title, count pill, hint, colour class);
  `ThingCardTests` (tile toggles collapse with aria-expanded, no chevron button,
  count pill text and aria-label, no header divider); `TaskRowTests` (round
  icons, star colour class); `PageHeadingTests` (subtitle beside title);
  `AreaChipsTests` (current chip tinted, `aria-current` kept); page tests updated
  to `pspad-section-heading`.
- Visual check in a browser is the maintainer's (no screenshot tooling).

## 5. Wrap-up (last plan task)

Fold §2–§3 into `ui-spec.md` (§1 component choice, §2 spacing, §4 visual &
theming including the decorative-line exemption), delete this file and its
AGENTS.md row.
