# Budget Overview Design

Redesign of the budget page. Overview replaces Month as the landing view and
answers "how is this month going"; Entries holds history for any month; Stats
replaces Summary with averages, where money goes and a next-month forecast.
The page uses the full content width on desktop and icon tabs on a phone.
Mockup: https://claude.ai/artifact/WiMbHYQDzNCgrnANGRUc6g (layout and content
only; styling follows the app, see `specs/ui-spec.md`). Builds on
`specs/money-design.md`; where the two disagree this document wins for the
budget page's views.

## 1. Scope

In:

- Five views in the URL: `overview` (default), `entries`, `balance`, `stats`,
  `settings`. `month` and `summary` keep working as aliases.
- A "usual month" baseline: averages over the last six full months, per
  category, computed in the browser from the replica. It feeds the Overview
  pace, the insights and the Stats forecast.
- Entries: any month, filter by kind and by category, search, sort by entry
  date or by recorded time, a "recorded" caption on each row, add an entry
  dated in the shown month.
- Any category name on the page opens Entries filtered to that category.
- Quick actions Add expense / Add income in the view row from 960px up; the
  FAB menu stays below that.
- Stats: a Last 6 months preset (default), per-month averages, forecast,
  income vs expenses per month as bars, category share, net worth by type.
- Balance and Settings use the width: two columns from 960px up.

Out:

- Budget limits, recurring entries, alerts or reminders.
- Any server-side statistics projection (money stays client-computed).
- New insight kinds beyond the four in §3.4.

## 2. Reading layer (`PSPad.Module.Money/Reading`, pure, shared)

### 2.1 `MoneySummary` additions

`MoneySummary.For(entries, snapshots, from, to)` gains:

- `PerMonth: IReadOnlyList<MonthFigure>` — `MonthFigure(YearMonth Month,
  decimal Income, decimal Expenses)` for every month of the range, in order.
  `MonthFigure.Net => Income - Expenses`.
- `SampledMonths: int` — months of the range with at least one live entry.
- `AverageIncome`, `AverageExpenses`, `AverageSaved: decimal?` — the totals
  divided by `SampledMonths`; `null` when it is 0. Averages are rounded to
  2 decimals, `MidpointRounding.AwayFromZero`.
- `Cumulative` and `CumulativeMonth` are removed (the cumulative line chart
  goes away).

### 2.2 `UsualMonth`

```csharp
public sealed record UsualMonth(
    YearMonth From, YearMonth To, int SampledMonths,
    decimal Income, decimal Expenses,
    IReadOnlyList<CategoryUsual> ExpenseCategories,
    IReadOnlyList<CategoryUsual> IncomeCategories)
{
    public const int Window = 6;
    public decimal Saved => Income - Expenses;
    public bool IsEmpty => SampledMonths == 0;
    public static UsualMonth For(IEnumerable<MoneyEntry> entries, YearMonth current);
    public CategoryUsual? Expense(string category);   // case-insensitive
}

public sealed record CategoryUsual(string Category, decimal Average, int MonthsSeen, int TypicalDay);
```

- The window is the six months before `current`: `From = current − 6`,
  `To = current − 1`. Entries dated inside the window and not deleted count.
- `SampledMonths` counts window months with at least one live entry (income
  or expense). Months without entries are skipped: `Income` and `Expenses`
  are the window totals divided by `SampledMonths`. `IsEmpty` when 0; then
  every figure is 0 and the lists are empty.
- Per category (`CategoryTotal.RankingOf` groups case-insensitively, keeping
  the first spelling): `Average` = category total ÷ `SampledMonths`;
  `MonthsSeen` = window months with an entry in that category;
  `TypicalDay` = the median day-of-month of the category's entries (the lower
  median for an even count). Lists are sorted by `Average` descending, ties by
  name (ordinal, ignoring case).
- All money values are rounded to 2 decimals, `MidpointRounding.AwayFromZero`.

### 2.3 `MonthPace`

```csharp
public sealed record MonthPace(
    YearMonth Month, int Day, int DaysInMonth, MonthTotals Totals, UsualMonth Usual,
    IReadOnlyList<CategoryPace> Categories)
{
    public decimal ShareOfMonthPassed => (decimal)Day / DaysInMonth;
    public decimal? ExpectedIncomeByNow;    // Usual.Income × ShareOfMonthPassed, null when Usual.IsEmpty
    public decimal? ExpectedExpensesByNow;  // same with Expenses
    public decimal? ShareOfUsualSpent;      // Totals.Expenses ÷ Usual.Expenses; null when Usual.IsEmpty or Usual.Expenses == 0
    public static MonthPace For(IEnumerable<MoneyEntry> entries, YearMonth month, DateOnly today);
}

public sealed record CategoryPace(string Category, decimal Spent, decimal? UsualByNow, decimal? UsualMonth)
{
    public decimal? Delta => Spent - UsualByNow;
}
```

- `For` computes `MonthTotals.For(entries, month)`, `UsualMonth.For(entries,
  month)`, `Day` = `today.Day` when `today` is in `month`, `DaysInMonth` when
  `month` is past, `0` when future. `DaysInMonth` = `month.LastDay.Day`.
- `Categories` is the union of the month's expense categories and the usual
  expense categories, each with `Spent` (0 when absent this month), `UsualByNow`
  = `Average × ShareOfMonthPassed` and `UsualMonth` = `Average` (both `null`
  when the category has no usual figure or the baseline is empty). Sorted by
  `Spent` descending, then `UsualMonth` descending, then name.
- Expected and usual-by-now values are rounded to 2 decimals.

### 2.4 `Insight`

```csharp
public enum InsightKind { PastUsual, RunningHigh, IncomeExpected, OnTrack }

public sealed record Insight(InsightKind Kind, string Category, decimal Amount, decimal Usual, int Day);

public static class Insights
{
    public const decimal HighRatio = 1.15m;
    public const decimal HighMinimum = 50m;
    public const decimal OnTrackTolerance = 0.10m;
    public const int Limit = 5;
    public static IReadOnlyList<Insight> For(MonthPace pace);
}
```

Rules, evaluated over `pace.Categories` (expenses) and
`pace.Usual.IncomeCategories`; an empty baseline gives an empty list:

1. **PastUsual** — `Spent > UsualMonth` and `UsualMonth > 0`. `Amount` =
   `Spent`, `Usual` = `UsualMonth`.
2. **RunningHigh** — not PastUsual, `UsualByNow > 0`, `Spent ≥ UsualByNow ×
   HighRatio` and `Spent − UsualByNow ≥ HighMinimum`. `Amount` = `Spent`,
   `Usual` = `UsualByNow`.
3. **IncomeExpected** — an income category with `MonthsSeen × 2 ≥
   SampledMonths`, no live income entry in that category this month, and
   `TypicalDay ≥ pace.Day`. `Amount` = `Average`, `Day` = `TypicalDay`.
4. **OnTrack** — `Spent > 0`, `UsualByNow > 0` and `|Spent − UsualByNow| ≤
   UsualByNow × OnTrackTolerance`. `Amount` = `Spent`, `Usual` = `UsualByNow`.

Order: PastUsual, RunningHigh, IncomeExpected, OnTrack; within a kind by
`Amount` descending. At most `Limit` insights. `Day` is 0 except for
IncomeExpected.

## 3. Screens

Everything uses MudBlazor and the existing `pspad-*` classes; new classes
follow the existing naming. Edits happen in side panels, one FAB per page.

### 3.1 Page

- `.pspad-budget-page` loses its 960px cap: the page fills the content area
  like the other pages.
- View row: `MudToggleGroup` with Overview, Entries, Balance, Stats, each
  `MudToggleItem` with `UnselectedIcon`/`SelectedIcon` (Outlined/Filled
  `Dashboard`, `List`, `AccountBalance`, `BarChart`) and `Text`; the gear stays
  the Settings button. The group has `CheckMark="true"`: in MudBlazor 9.11
  that is what makes the item icons render (as `svg.mud-toggle-item-check-icon`
  inside `.mud-button-label`). Below 600px the items stack icon over text
  (`.pspad-budget-views .mud-toggle-item .mud-button-label` CSS only, the icon's
  side margin removed). From 960px the group is at most 560px.
- Quick actions `.pspad-budget-quick` (two `MudButton`s, "Expense" with the
  `Remove` icon outlined, "Income" with `Add` filled) sit at the right end of
  the view row on Overview and Entries with Mud's `d-none d-md-flex` (shown from
  960px); the FAB menu on those views has `d-md-none` (hidden from 960px). Both
  are absent on an archived budget. On Overview they open `EntryPanel` for
  today; on Entries for the shown month (§3.3).
- URL tabs: `overview`, `entries`, `balance`, `stats`, `settings`; `month` is
  read as `entries`, `summary` as `stats`. `BudgetLinks.ForTab` is unchanged.
- Month row `.pspad-month-row`: on Entries and Balance only, a flex row
  directly under the view row (`mb-3`) holding `MonthStrip`, which fills the
  row. The view row never holds a month control.
- `MonthStrip` (replaces `MonthSwitcher`): `.pspad-month-strip-scroll` is a
  horizontally scrolling row of `MudButton` chips `.pspad-month-chip`, one per
  month from the current month back eleven months, newest at the right edge.
  The row is laid out `direction: rtl` with the chips newest-first in the
  DOM, so a scrolling strip opens at the newest month without script; chips
  are `direction: ltr`. When the chips fit, `justify-content: safe center`
  centres them. Each chip shows the three-letter month and, on January and
  on the oldest chip, the year (`.pspad-month-chip-year`); `aria-label` is
  the full "September 2026". The shown month is `Variant.Filled` with
  `aria-pressed="true"`; the current month has `.pspad-month-chip-current`
  (a primary ring) and `aria-current="date"`; months in `Marked` (entries on
  Entries, live snapshots on Balance, computed by `BudgetPage`) have
  `.pspad-month-chip-marked`, which shows the dot `.pspad-month-chip-dot`.
  A shown month outside the window gets one extra chip at its end of the
  strip. `.pspad-month-more` (`CalendarMonth`, "Pick another month") opens
  the existing month sheet (`.pspad-month-sheet`, year arrows, twelve
  months, "This month", "Cancel").
- Settings button: `MudFab` `.pspad-budget-settings.pspad-fab-settings`
  (`Size.Small`, `Color.Default`, `Settings` icon, aria-label "Budget
  settings", `aria-pressed` on the settings view) fixed bottom-left, 24px
  from the edges, offset by the open drawer from 960px like the snackbar;
  16px and above the phone navigation bar under 600px. The view row holds
  only the views, the members badge and the quick actions.
- `BudgetPage` keeps one `_category` filter in memory (`string?`). `ShowCategory(name)`
  sets it and navigates to Entries. It is cleared by the chip on Entries and
  when the budget id changes. It is not in the URL.
- Recorded times are shown in the user's stored time zone
  (`LocalToday.Zone(State.TimeZone)`), via `MoneyText.Recorded(at, zone, today)`:
  "today 18:40", "yesterday 21:14", else "3 Oct 09:00", with the year when
  it differs from today's ("3 Oct 2025 09:00").

### 3.2 Overview (`BudgetOverviewTab`, `.pspad-overview`)

Always the current month (`State.Today`). `MudGrid Spacing="4"`:

- **Hero** (`xs=12 md=8`, `.pspad-overview-hero`): `MoneyHero` with Label
  "Net so far" and the signed net; figures Income (caption
  `.pspad-overview-income-usual` "usual by now {X}"), Expenses (caption
  `.pspad-overview-expenses-delta` "+{Δ} vs usual by now" red when over, "−{Δ}"
  otherwise), "Expected month end" (`≈ {Usual.Expenses}`, caption "avg of last
  {MoneyText.MonthCount(SampledMonths)}" — "1 month", "6 months"). Footer: a `MudProgressLinear`
  (`.pspad-overview-pace`, `aria-hidden`) with Value = `ShareOfUsualSpent × 100`
  capped at 100 and a caption line "Spent {P}% of a usual month · {Q}% of the
  month passed", then the `CategoryBar`. A heading caption above the hero reads
  "{Month yyyy} · day {Day} of {DaysInMonth}" (`.pspad-overview-day`).
  With an empty baseline the captions, the expected figure and the pace are
  replaced by one line `.pspad-overview-no-usual`: "Averages appear after a
  full month of entries."
- **Worth a look** (`xs=12 md=4`, `.pspad-overview-insights`): a `MudPaper`
  with a `SectionHeading` "Worth a look" and one `MudButton`-like row per
  insight (`.pspad-insight`, `data-kind`), each with a `MudIcon` (TrendingUp
  for PastUsual and RunningHigh, Info for IncomeExpected, Check for OnTrack),
  a title and a caption:
  - PastUsual: "{Category} already past a usual month" / "{Amount} vs {Usual} average"
  - RunningHigh: "{Category} is running high" / "{Amount} so far, {Amount−Usual} more than usual at this point"
    (`Insight.Day` is 0 for this kind)
  - IncomeExpected: "{Category} usually lands around the {Day}" / "{Amount} on average; nothing yet this month"
  - OnTrack: "{Category} on track" / "{Amount}, close to usual"
  Clicking an insight with an expense category opens Entries filtered to it;
  IncomeExpected rows are plain `div.pspad-insight` rows with the list item's
  look, not clickable and not focusable. Without insights: "Nothing unusual so far." (`.pspad-overview-quiet`).
- **Where money went** (`xs=12 md=5`, `.pspad-overview-categories`): one row
  per `CategoryPace` with `Spent > 0` (`.pspad-category-pace`): a `MudButton`
  (Variant Text, `.pspad-category-open`) with the name, a `MudProgressLinear`
  (`aria-hidden`, Max = the largest `Spent` or `UsualMonth`), the amount and,
  when `UsualByNow` is set, the delta caption (`.pspad-category-delta`, red
  when positive). Without expenses: "No expenses yet in {Month yyyy}."
- **Recent entries** (`xs=12 md=4`, `.pspad-overview-recent`): the five live
  entries with the latest `RecordedAt` from any month, as `EntryRow`s with
  `Recorded` captions, and a text button "All {Month yyyy} entries"
  (`.pspad-overview-all-entries`) that opens Entries on the current month.
  Without entries: "No entries yet."
- **Net worth** (`xs=12 md=3`, `.pspad-overview-net-worth`): `MoneyTile`-like
  card with the latest snapshot on or before the current month: label "Net
  worth · {Month yyyy}", the value, caption "{±Δ} vs {Prev Month yyyy}" when a
  previous one exists. When the current month has no snapshot and the budget
  isn't archived: a line "{Month yyyy} has no balance yet" and a "Start {Month
  yyyy}" button (`.pspad-overview-start-month`) sending `StartBalanceMonth`.
  Without any snapshot and archived: "—" and "No balance yet".

### 3.3 Entries (`BudgetEntriesTab`, `.pspad-entries`)

Replaces `BudgetMonthTab`. The month row (§3.1) sits above the toolbar.

- Toolbar `.pspad-entries-toolbar`, one line from 600px: the kind switch
  `.pspad-entry-filter` (a `MudToggleGroup` of All / Expenses / Income, small,
  primary, same look as the view switch; full width under 600px), the category chip `.pspad-entries-category` (a `MudChip` with
  `OnClose` that clears the filter; shown only when set), a `MudTextField`
  `.pspad-entries-search` (Placeholder "Search name or note", Immediate,
  Clearable, Adornment Search), and a `MudSelect<EntrySort>`
  `.pspad-entries-sort` with "Entry date" (default) and "Recorded".
- Caption `.pspad-entries-caption`: "{N} entries · income {I} · expenses {E} ·
  net {±net}" over the filtered rows; "No entries in {Month yyyy}." when the
  month has none at all; "No entries match." when filters hide them all.
- The list: by **entry date** it is today's day groups (`EntryDay`, newest
  first, day net in the heading). By **recorded** the groups are recorded
  days in the user's zone, newest first, heading "Recorded {day}", with the
  group's net; rows inside are by `RecordedAt` descending.
- Every `EntryRow` gains a `Recorded` caption parameter: when set, the grey
  line ends with " · recorded {MoneyText.Recorded}" (`.pspad-entry-recorded`).
  The category in the grey line becomes a button `.pspad-entry-category` that
  sets the category filter (`OnOpenCategory`).
- Search matches name or note, case-insensitive, trimmed; empty means all.
- The filter state (`kind`, `category`, `search`, `sort`) lives in
  `BudgetPage` so it survives month changes; `category` is the shared one of §3.1.
- Quick actions and the FAB on Entries open `EntryPanel` with
  `EntryQuery.ForNew(uri, budgetId, kind, month)` → `?expense={id}&month=2026-09`.
  `EntryQuery.NewFrom` returns `(BudgetId, Kind, YearMonth? Month)`; `AppShell`
  passes `Month` to `EntryPanel.NewMonth`. `EntryPanel` defaults the date to
  today when `NewMonth` is null or contains today, to the month's last day
  when it is past, and to its first day when it is future.
- The totals hero of the old Month tab is gone from Entries (Overview has it).

### 3.4 Balance (`BudgetBalanceTab`)

Unchanged behaviour; the layout becomes `MudGrid`: the hero and a new
`MudPaper` `.pspad-balance-history` ("Last 12 months", a `MudChart`
StackedBar of `MoneySummary.For([], snapshots, (current−11).FirstDay,
current.LastDay).NetWorth` by type with the `mud-sr-only` table as in
`BudgetSummaryCharts`; hidden when every month is a gap) in `xs=12 lg=4`;
the holdings in `xs=12 lg=8`. Inside the `lg=4` column an inner `MudGrid`
puts the hero in `xs=12 md=5 lg=12` and the history in `xs=12 md=7 lg=12`,
so between 960px and 1279px they sit side by side above the holdings.

A month without a snapshot keeps the same layout, with no banner: the hero
slot holds `MudPaper` `.pspad-balance-missing` ("{Month} has no balance yet",
then "Starting it copies the holdings from {month of `Totals.Previous`}." or
"Start it and add your holdings." when there is none, then the
`.pspad-start-month` button "Start {Month}" unless read-only); the holdings
column shows `.pspad-balance-empty` "No holdings in {Month}.".

The FAB on Balance is a `MudFabMenu` `.pspad-fab` (activator "Month actions")
with "Add holding" (`.pspad-fab-add-holding`) and "Delete month"
(`.pspad-delete-month`, error colour), shown only with a snapshot on a live
budget. There is no month menu in the month row.

Holdings columns follow the holdings box, not the viewport: `.pspad-holdings`
is `container-type: inline-size`; `@container (min-width: 640px)` lays
`.pspad-holdings-head` and `.pspad-holding-row` out as the five-column grid
(`minmax(0, 2fr) repeat(4, minmax(0, 1fr))`), `@container (max-width: 639.98px)`
hides `.pspad-holding-cell` and keeps the stacked phone row. The head cells
are `white-space: nowrap`.

### 3.5 Stats (`BudgetStatsTab`, `.pspad-stats`)

Renamed from Summary (`BudgetSummaryTab` → `BudgetStatsTab`,
`BudgetSummaryCharts` → `BudgetStatsCharts`; CSS prefix `pspad-summary-` →
`pspad-stats-`).

- Presets: **Last 6 months** (default; `SummaryPreset.LastSixMonths`:
  `(current−6).FirstDay` to `(current−1).LastDay`), This year, Last 12 months,
  Last year, Custom. The caption shows the range plus " · {MonthCount(SampledMonths)}
  with entries".
- KPI row (`.pspad-stats-kpis`, four `MoneyTile`s): "Avg income / month"
  (`.pspad-stats-avg-income`), "Avg expenses / month" (`.pspad-stats-avg-expenses`),
  "Avg saved / month" (`.pspad-stats-avg-saved`, red when negative), "Savings rate"
  (`.pspad-stats-rate`). The income and expenses tiles' captions show "lowest
  {min} · highest {max}" over `PerMonth` months with entries; the saved tile's
  caption is "saved in {N} of {M} months" (N months with a positive net, M =
  `SampledMonths`). With no sampled months every caption is "—".
- **Forecast** (`.pspad-stats-forecast`, `xs=12 md=5`): "{Next month yyyy}
  forecast", hint "avg of last {MonthCount(SampledMonths)}"; figures Expected
  expenses, Expected income, Expected saved from `UsualMonth.For(entries,
  current)` (always the six-month window, independent of the preset); then
  one row per `ExpenseCategories` item with a `MudProgressLinear` and the
  average, the name a `.pspad-category-open` button. Empty baseline: "A
  forecast needs at least one full month of entries."
- **Income vs expenses by month** (`xs=12 md=7`, `.pspad-stats-by-month`):
  `MudChart` `ChartType.Bar` with series Income and Expenses from `PerMonth`,
  and the `mud-sr-only` table (Month, Income, Expenses, Net).
- **Where money goes** (`xs=12 md=5`, `.pspad-stats-ranking`, card title
  "Where money goes"): the ranking as today plus a share caption "{P}%" of `Expenses` per row; names are
  `.pspad-category-open` buttons.
- **Spend by category per month** (`xs=12 md=7`, `.pspad-stats-categories`):
  the existing stacked bar (top 4 + Other).
- **Net worth by holding type** (`xs=12`, `.pspad-stats-net-worth-chart`): the
  existing stacked bar, drawn by the shared `NetWorthByTypeChart` (Balance's
  history card uses it too, at 200px).
- The Saved hero and the Net worth tile of the old Summary are removed
  (Overview carries net worth; the KPI row carries saved).

### 3.6 Settings (`BudgetSettingsTab`)

`MudGrid`: `xs=12 lg=7` holds the two `CategoryList`s side by side from 600px
(`MudGrid` inside, `sm=6` each); `xs=12 lg=5` holds Budget, Sharing and the
two Import cards (Budget first) in an inner `MudGrid` of `xs=12 sm=6 lg=12`
items, so below 1280px the cards pair up under the lists. `CategoryList`
names become `.pspad-category-open` buttons calling `OnOpen(name)`; the tab
forwards expense names to `OnOpenCategory` (income names open Entries
filtered too — the filter matches either kind by name).

A `CategoryList` row (`.pspad-category-row`) is a plain `<button>`
`.pspad-category-open` styled like `.pspad-holding-row` (no button chrome,
hover tint on the row, name and caption stacked left) holding
`.pspad-category-name` and, when the category has entries, a caption line
`.pspad-category-usage` ("1 entry" / "N entries") under the name, followed by
one `MudMenu` `.pspad-category-menu` (`MoreVert`,
aria-label "Actions for {name}") with `MudMenuItem`s `.pspad-category-rename`,
`.pspad-category-merge` (disabled with fewer than two categories) and
`.pspad-category-remove` (disabled while the category has entries). The
menu is absent on an archived budget. Rename and merge still swap the row
for the inline input or select as before.

### 3.7 Phone

Cards stack (`xs=12`). The view row stacks icons over labels under 600px.
The month strip swipes sideways. The FAB menu stays; the settings FAB sits
bottom-left. Nothing else changes.

## 4. Testing

Every class `[UnitTest]`. Fixtures: `Given` (module), `MoneyEntries`,
`Balances`, `BudgetsPageTests.Named` (app).

- Module: `UsualMonthTests` (window, skipped empty months, averages, per
  category, typical day median, case-insensitive merge, empty), `MonthPaceTests`
  (day/days for current, past and future month, expected-by-now, share,
  categories union and order), `InsightsTests` (each kind, ordering, limit,
  empty baseline, thresholds), `MoneySummaryTests` additions (`PerMonth`,
  `SampledMonths`, averages, cumulative removed).
- App: `BudgetPageTests` (new view names and URLs, aliases, quick actions
  present from the view row, hidden when archived), `BudgetOverviewTests`
  (hero figures and captions, no-baseline line, insights text, category click
  opens Entries filtered, recent entries, net worth card and Start month),
  `BudgetEntriesTests` (kind, category chip clears, search, sort by recorded
  groups, caption totals, add in month passes `month`), `EntryPanelTests`
  (default date from `NewMonth`), `EntryQueryTests` (`month` round trip),
  `BudgetStatsTests` (default preset, KPIs, forecast, bar chart data, share
  captions), `BudgetBalanceTests` (history chart present / hidden),
  `BudgetPageTests` settings category click, `MoneyTextTests` (`Recorded`).
- Layout follow-up: `BudgetPageTests` (month row holds the switcher, the
  view row does not; category menu opens rename, merge, remove; remove
  disabled while used; `lg` grid classes on Settings), `BudgetBalanceTests`
  (`lg` grid classes, month menu in the month row), `StylesheetRulesTests`
  (holdings container query, month-control widths, nowrap head).
- `StylesheetRulesTests` if it asserts on budget CSS.

## 5. Docs

`docs/src/pages/features.astro` Budgets body: Month/Summary wording →
"an Overview of the month against your usual spending, entries for any month,
a monthly balance …, and Stats with averages, a next-month forecast and charts
over any range". `docs/src/pages/index.astro` Budgets body gets "a forecast
from your last six months". `specs/ui-spec.md` Budgets paragraph and
`specs/money-design.md` §6 get a pointer to this document.
