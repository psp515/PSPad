# Money Design

Issue #109. Track expenses, incomes and monthly balances in budgets, with PLN
as the base for every total, and the whole thing working offline. Decisions:
`adr/0059` (the module), `adr/0060` (navigation). Mockups: the "PSPad
Budgets" design canvas (https://claude.ai/artifact/Qfud3qPJCZfVLjbCqgJk42).

## 1. Scope

In:

- Several budgets per user. A budget is archived, never deleted.
- Expenses and incomes, each with a name, a category, money and a date.
- Separate expense and income category lists per budget. Seeded on
  creation, editable, and created on the fly from the entry panel.
- Monthly balance snapshots: named holdings (cash, savings, investment) in
  any currency, carried forward month to month.
- A rate to PLN frozen on every amount. Pre-filled from the last rate used in
  the budget, or fetched from NBP on demand.
- Month, Balance, Summary and Settings tabs; charts on Summary.
- Default currency per user, set in Settings.
- Budgets in the sidebar and in the phone bottom bar.

Out (each its own issue):

- Sharing a budget with other users (#121, see §9). The model is built for it.
- Public snapshots of money. Never.
- Investment detail (units, buy price, gain on the asset itself).
- Recurring expenses, budget limits, imports from bank files.
- Statistics projections of money events.

## 2. Module

`src/modules/PSPad.Module.Money`. It references `PSPad.Abstractions` only,
so it compiles to WASM and its handlers run in the browser and on the server,
like Tasks (AD-3, AD-4). No module references it except `PSPad.Api` and
`PSPad.App`. It knows nothing about Tasks, Statistics or Presentation.

Folders follow the one-type-per-file, grouped-by-operation rule:

```
Budgets/        Budget.cs, BudgetAccess.cs, CategoryKind.cs, CategoryName.cs, CategoryCascade.cs,
                Create/, Rename/, Archive/, Restore/, AddCategory/, RenameCategory/,
                MergeCategory/, RemoveCategory/ (categories live inside the Budget aggregate)
Entries/        MoneyEntry.cs, MoneyEntryCascade.cs, RecordExpense/, RecordIncome/, Edit/,
                Delete/, Recategorise/ (an entry's kind is CategoryKind)
Balance/        BalanceSnapshot.cs, Holding.cs, HoldingType.cs, StartMonth/,
                AddHolding/, EditHolding/, RemoveHolding/, DeleteMonth/
Preferences/    MoneyPreferences.cs, SetDefaultCurrency/
Values/         Currencies.cs, YearMonth.cs, YearMonthJsonConverter.cs, Money.cs (namespace
                PSPad.Module.Money: a type named Money under …Money.Values would be shadowed
                by the namespace)
Reading/        RateSuggestion.cs, MonthTotals.cs, CategoryTotal.cs, EntryDay.cs, BalanceTotals.cs,
                MoneySummary.cs, CumulativeMonth.cs, CategorySeries.cs, NetWorthMonth.cs
```

## 3. Domain model

### 3.1 Values

`Money(decimal Amount, string Currency, decimal RateToPln, DateOnly RateDate)`

- `Amount > 0`. The entry kind gives the sign, never the number.
- `Currency` is an upper-case ISO-4217 code from `Currencies.All`, a fixed
  list in the module.
- `RateToPln > 0`. For PLN it is forced to `1` and `RateDate` is the money's
  own date.
- `InPln` = `Math.Round(Amount × RateToPln, 2, MidpointRounding.AwayFromZero)`.
  It is computed, never stored.
- Stored as BSON `Decimal128`, never `double` or string.

`YearMonth` is a `yyyy-MM` value with ordering, `FirstDay`, `LastDay` and
`Parse`. JSON carries it as text, e.g. `"2026-10"` (`YearMonthJsonConverter`),
so commands and events hold the month as text.

A category is a trimmed name of 1–40 characters. Names are case-insensitive
unique within one kind of one budget, and the stored spelling is the first
one used.

### 3.2 Aggregates

| Aggregate | Id | Fields | Collection |
|---|---|---|---|
| `Budget` | Guid | `UserId` (owner), `Name`, `ExpenseCategories[]`, `IncomeCategories[]`, `CreatedAt`, `ArchivedAt?` | `budgets` |
| `MoneyEntry` | Guid | `UserId`, `BudgetId`, `Kind` (`CategoryKind`: Expense, Income), `Name`, `Category`, `Money`, `Date`, `Note?`, `RecordedAt`, `Deleted` | `moneyentries` |
| `BalanceSnapshot` | `IdFor(budgetId, month)` | `UserId`, `BudgetId`, `Month` (stored as the text `_month`, e.g. `"2026-10"`: the BSON driver can't deserialize a struct), `Holdings[]`, `UpdatedAt` (the last event's time), `Deleted` | `balancesnapshots` |
| `MoneyPreferences` | `IdFor(userId)` | `UserId`, `DefaultCurrency` (default `PLN`) | `moneypreferences` |

`Holding(Guid HoldingId, string Name, HoldingType Type, Money Money)`, where
`Type` is Cash, Savings or Investment and `Name` is trimmed, 1–80 characters.
A holding's money follows the `Money` rules with the month's last day as its
own date, so a PLN holding is always dated the last day. A foreign holding's
`RateDate` is the day its rate is from (see §6 `HoldingPanel`). `IdFor` XORs
the budget id with a fixed salt and the month, like `MoneyPreferences.IdFor`.

`UserId` on every Money document is the budget's owner, always. That is the
same rule shared lists use (`adr/0054`), so sharing can be added later
without rewriting ownership.

### 3.3 Rules

**Budgets**

- `CreateBudget` seeds 8 expense categories (Home, Food, Eating Out, PC, Bike,
  Car, Gifts, Clothes) and 5 income categories (Salary, Freelance, Interest,
  Gifts, Other).
- A name is required, 1–80 characters.
- `ArchiveBudget` sets `ArchivedAt`, and `RestoreBudget` clears it. Each is a
  no-op when the budget is already in that state.
- There is no delete.
- An archived budget is read-only. Every command that writes to it, its
  categories, its entries or its snapshots is rejected with "This budget is
  archived." The one exception is `RestoreBudget`.

**Access**

- `BudgetAccess.To(budget, actorId)` gates every command except
  `CreateBudget` and `SetDefaultCurrency`.
- v1 grants access to the owner only. Sharing widens this one function.
- A missing budget is rejected the same way a missing list is (`adr/0042`).

**Categories**

- `AddCategory(kind, name)` is a no-op if the name already exists
  (case-insensitive). Two offline devices adding "Hobby" therefore converge.
- `RenameCategory(kind, from, to)` is rejected if `to` already exists; the
  user should merge instead. A case-only rename of the same category is allowed. It emits `CategoryRenamed` and one
  `MoneyEntryRecategorised` per live entry of that kind in the budget, all in
  one unit of work (the `adr/0042` cascade pattern).
- `MergeCategory(kind, from, into)` relabels the same way and emits one
  `CategoriesMerged`, whose fold removes `from`.
- `RemoveCategory(kind, name)` is rejected while any live entry uses the name.
- Merging a category into itself, or naming a category that doesn't exist, is
  rejected.

**Entries**

- `RecordExpense` and `RecordIncome` carry `EntryId`, `BudgetId`, `Name`
  (1–120 characters), `Category`, `Money`, `Date` and `Note?`.
- A category the budget doesn't have yet is added within the same command
  (`CategoryAdded` plus `ExpenseRecorded`). This is how the entry panel's
  "Create category" works.
- `EditEntry` replaces name, category, money, date and note. It auto-adds a
  new category the same way.
- `DeleteEntry` is a soft delete, like other aggregates.
- The rate on an entry is whatever the command carries. Nothing on the server
  recomputes it later.

**Balance**

- `StartBalanceMonth(budgetId, month)` is a no-op if a live snapshot already
  exists for that month, so two devices starting the same month offline
  converge. An existing snapshot of another budget is rejected ("That budget
  does not exist.").
  - Otherwise it copies the holdings of the latest earlier live snapshot
    unchanged, keeping their `HoldingId`s, rates and `RateDate`s. A rate date
    is the day the rate is from, so `RateSuggestion`'s latest-wins stays
    honest.
  - With no earlier snapshot, it starts empty.
  - Starting a month earlier than an existing one is allowed, and copies from
    whatever precedes it.
- `AddHolding`, `EditHolding` and `RemoveHolding` act on one holding of an
  existing snapshot.
- `DeleteBalanceMonth` soft-deletes the snapshot. Starting the month again
  afterwards revives the same id with fresh holdings.
- Every balance command carries `BudgetId` and `Month`. Its handler loads
  the budget first and the snapshot `IdFor(budgetId, month)` second, and
  access is checked before any snapshot rule, so a stranger's budget reads
  "That budget does not exist." like a missing one.
- `AddHolding`, `EditHolding`, `RemoveHolding` and `DeleteBalanceMonth` need
  a snapshot for that month ("That month has no balance yet."). Deleting a
  deleted month again is a no-op; an edit that changes nothing emits nothing.
- A holding id is unique within its snapshot ("That holding already
  exists."). Carried holdings keep theirs, so one id can appear in several
  months.

**Preferences**

- `SetDefaultCurrency(currency)` must use a code from `Currencies.All`. It
  only preselects the currency in the panels and never changes any total.

**Offline edge.** Device A renames Food → Groceries while device B, offline,
records an expense under Food. When B syncs, `RecordExpense` auto-adds Food
again. That is accepted: the user sees both categories and merges them. Last
write wins per aggregate (AD-5); there is no cross-aggregate merge.
Two devices that start the same month offline converge: the second
`StartBalanceMonth` is a no-op when it syncs, and its holding commands land
in the first device's snapshot.

### 3.4 Events

`BudgetCreated`, `BudgetRenamed`, `BudgetArchived`, `BudgetRestored`,
`CategoryAdded`, `CategoryRenamed`, `CategoryRemoved`, `CategoriesMerged`,
`ExpenseRecorded`, `IncomeRecorded`, `MoneyEntryEdited`,
`MoneyEntryRecategorised`, `MoneyEntryDeleted`, `BalanceMonthStarted`,
`HoldingAdded`, `HoldingEdited`, `HoldingRemoved`, `BalanceMonthDeleted`,
`DefaultCurrencySet`.

Each event carries the full values the aggregate needs to fold, the way
`adr/0037` asks, so a future consumer can read the log without loading
documents.

## 4. Reading (pure, in the module, shared by client and server)

**`RateSuggestion.For(currency, entries, snapshots)`**
- It returns `RateSuggestion(RateToPln, RateDate?)`. Slice 2 ships
  `For(currency, entries)`; slice 3 adds the `snapshots` overload. The entry
  panel keeps a suggested rate's `RateDate`; an NBP rate carries NBP's
  `effectiveDate`; a typed rate takes the entry's date.
- PLN → `1`.
- Otherwise, the `RateToPln` with the latest `RateDate` across the budget's
  live entries and holdings in that currency; ties go to the newest
  document (an entry's `RecordedAt`, a snapshot's `UpdatedAt`). The entry
  and holding panels both use this overload.
- None → no suggestion, and the rate field is required.

**`MonthTotals.For(entries, month)`**
- Income, expenses and net in PLN.
- Expenses per category, sorted by value descending.
- Entries grouped by date, newest first.

**`BalanceTotals.For(snapshots, month)`** (one budget's snapshots)
- The month's live snapshot, and the latest earlier live one as `Previous`.
- Net worth: the holdings' `InPln` at their frozen rates, summed; 0 without
  a snapshot.
- Change: net worth minus `Previous`'s; `null` without either.
- `Latest(snapshots, onOrBefore)`: the newest live snapshot on or before a
  month, for the budget cards.
- `ValueToday(holding, rates)` and `AtRates(snapshot, rates)` value holdings
  at the NBP rates the Balance tab fetched. PLN needs no rate; `AtRates` is
  `null` while any currency lacks one.

**`MoneySummary.For(entries, snapshots, from, to)`** (one budget's documents)
- `from` and `to` are days, both inclusive; the months run from `from`'s
  month to `to`'s. A range that ends before it starts is rejected.
- Income, expenses, saved (income − expenses), and savings rate (saved ÷
  income; `null` when income is 0, negative when spending exceeds income).
- Cumulative income and expenses per month (`CumulativeMonth`), running from
  0 at the first month.
- Expenses per month per category (`CategorySeries`): the top 4 categories in
  the range keep their own series, and the rest go into "Other" (into the
  top-4 category itself when one is called "Other").
- Net worth per month by holding type (`NetWorthMonth`: Cash, Savings,
  Investment), at snapshot rates. A month without a live snapshot is a gap
  (all `null`), not carried forward.
- Category ranking: every expense category in the range, largest first, ties
  by name, without an "Other" bucket. `CategoryTotal.RankingOf` does the
  ranking for both `MonthTotals` and `MoneySummary`.
- Latest net worth: the newest live snapshot on or before `to`'s month, even
  when it is older than `from`.

"Today" for default dates and the current month is the user's stored time
zone, never machine-local (the AGENTS.md §2 invariant).

## 5. Sync, storage and API

- **Sync.** `budgets`, `moneyentries`, `balancesnapshots` and
  `moneypreferences` join delta sync by `seq` (AD-6), filtered by `UserId`.
  They are added to `SyncReader`, to `SyncService`'s collection map and to
  `MongoIndexes`. Existing replicas pull them from zero once (`adr/0049`).
  The client needs no new store: the replica keeps every collection in its one
  generic `documents` store, indexed by type and user.
- **Commands** go through `/api/commands` and the outbox. Money's handlers are
  registered in both hosts. Rejections surface the usual way (AD-5,
  `adr/0021`).
- **Account deletion.** `UserDataWipe` is generic (`adr/0034`, `adr/0041`). A
  test asserts that it wipes the four new collections.
- **NBP lookup**: `GET /api/money/nbp-rate?currency=EUR&date=2026-10-09`
  - It is authenticated, it's not a command, and it's server-only (in
    `PSPad.Api`, not the module).
  - It calls `https://api.nbp.pl/api/exchangerates/rates/A/{code}/{date−7d}/{date}/?format=json`
    and returns the last rate on or before `date`:
    `{ currency, rate, effectiveDate }`. NBP publishes nothing on weekends or
    holidays, hence the 7-day window.
  - The timeout is 5 s. An unknown currency (without calling NBP), a date
    before 2002-01-02, no rate or any NBP 4xx gives `404`; NBP 5xx, timeout,
    network error, non-JSON or a response without rates gives `502`. Only
    successes are cached.
  - Results are cached in memory per `(code, date)`: 24 h for past dates,
    1 h for today.
  - The client disables the NBP button while offline and shows a snackbar on
    any failure. The field keeps its value.
  - `PLN` never calls it.
- **Self-hosting.** The API container needs outbound HTTPS to `api.nbp.pl`
  for the NBP button only. Everything else works without it. There is no new
  env var or port. This gets a note in `docs/src/pages/install.astro`.

## 6. Screens

The canvas holds the mockups; this section is the contract. Everything uses
MudBlazor, edits happen in side panels (`adr/0052`), and each page has one
FAB (`adr/0033`).

**Page header.** Like the area screen: an icon tile, a title and a one-line
subtitle. No app bar.

**`/budgets` (`Pages/Budgets.razor`)**
- The header subtitle reads "N budgets · M archived · totals in PLN".
- A "Show archived" switch, off on each visit (not remembered). Archived
  cards are dimmed and open read-only.
- Each card shows the current month's income, expenses and net and an
  expense/income bar. Below them: "Net worth · {Month yyyy}" with the value
  of the newest live snapshot on or before the current month, or, without
  one, a "Start {current Month yyyy}" link to the Balance tab
  ("No balance yet" on an archived card). A card never writes.
- FAB: New budget, which opens a side panel asking for the name.
- With no budgets at all, there's an empty state with a "Create budget"
  button.

**`/budgets/{id}/{tab?}` (`Pages/BudgetPage.razor`)**, with `MudTabs` for
Month (default), Balance, Summary and Settings. The tab lives in the URL.

- **Month**
  - ‹ month › switcher.
  - Income, Expenses and Net tiles.
  - A stacked bar of expenses by category, with a legend.
  - Entries grouped by day. A row shows name, category chip and amount; a
    non-PLN row also shows "−X PLN @ rate".
  - Tapping a row opens `EntryPanel`.
  - FAB menu: Add expense / Add income.
- **`EntryPanel`** fields:
  - name
  - category: a `MudAutocomplete` over that kind's list, offering "Create
    category "…"" when nothing matches
  - amount
  - currency, preselected from `MoneyPreferences`
  - rate to PLN, prefilled by `RateSuggestion`, with an NBP button for the
    entry date; hidden for PLN. Amount, currency, rate and NBP are the shared
    `RateFields`, which restart for each entry that opens.
  - a read-only "In PLN" value
  - date, defaulting to today in the user's time zone
  - note
  - Save, Cancel (the panel's close button), and Delete when editing.
  - A second save is ignored while one is sending. For an archived budget the
    panel is read-only.
- **Balance**
  - Month switcher, shared with the Month tab.
  - With no snapshot: a "{Month yyyy} has no balance yet" banner with a
    "Start {Month yyyy}" button (no button when archived), and no FAB.
  - Tiles: Net worth; At NBP today; Currency difference (at NBP today minus
    net worth); and "vs {Month yyyy}" against the latest earlier snapshot
    ("vs previous month" and "—" without one).
  - The two NBP tiles call the lookup for today once per foreign currency
    per visit, only while online. They are hidden offline and while any
    currency lacks a rate. A failed lookup isn't retried during the visit and
    shows no snackbar; an answer for another currency is dropped. Nothing is
    stored. A PLN-only balance shows them without any lookup.
  - Holdings table (`MudTable`): name and type chip, amount and currency,
    rate, PLN, and "Today" (the difference at today's NBP rate; "—" for PLN
    or a missing rate).
  - FAB: Add holding, only when the month has a snapshot and the budget isn't
    archived. Tapping a row
    opens `HoldingPanel` (`?balance={snapshotId}&holding={holdingId|new}`).
  - Overflow menu ("Month actions"): Delete month, only when the month has a
    snapshot and the budget isn't archived. No confirmation; starting the
    month again revives it.
- **`HoldingPanel`**: name, type (a three-way `MudToggleGroup`: Cash,
  Savings, Investment), amount, currency (preselected from
  `MoneyPreferences`), rate with an NBP button, "In PLN", Remove, Cancel (the
  close button) and Save.
  - The rate is prefilled by `RateSuggestion` over the budget's entries and
    holdings and hidden for PLN. A suggested rate keeps its date, an NBP rate
    takes NBP's `effectiveDate`, and a typed rate is dated the month's last
    day, or today while the month hasn't ended.
  - The NBP button asks for that same day: the month's last day, or today
    while the month hasn't ended (the endpoint rejects later dates). It is
    disabled offline; a failure shows the same snackbar as the entry panel;
    an answer that arrives after the currency or the open holding changed is
    dropped.
  - Amount, currency, rate, NBP and "In PLN" are the same `RateFields`
    component the entry panel uses; its fields restart for each entry or
    holding that opens.
  - Read-only for an archived budget. A second save is ignored while one is
    sending.
- **Summary**
  - Computed in the browser from the replica, so it works offline. No FAB,
    no writes; an archived budget shows the same Summary.
  - Range presets in a `MudToggleGroup`: This year (default; 1 January to
    today), Last 12 months (the first of the month eleven months back to
    today), Last year (the whole previous calendar year) and Custom (a
    `MudDateRangePicker`, applied once both ends are picked). "Today" is the
    user's stored time zone. The range is not in the URL and is This year on
    each visit. A caption shows it, e.g. "1 Jan 2026 – 9 Oct 2026".
  - Tiles: Income, Expenses, Saved, Savings rate (a whole percent, "—"
    without income), and "Net worth · {Month yyyy}" of the latest snapshot
    ("Net worth" and "—" without one).
  - Three `MudChart`s: cumulative income vs expenses (line), expenses by
    month by category (stacked bar), net worth by holding type (stacked bar;
    a gap month draws as 0). Category ranking is a `MudProgressLinear` per
    category with its name and amount, as Statistics' "Where the work went"
    does (MudBlazor 9.9 has no horizontal bar chart).
  - Each chart has a visually hidden table fallback for screen readers, as
    Statistics does; a gap reads "—". Without expenses the two category
    cards say "No expenses in this range."; without any balance in the range
    the net worth card says "No balances in this range.".
- **Settings**
  - Expense and income category lists. Each row has rename, merge and
    remove; remove is disabled while the category has entries, which the row
    shows as "N entries".
  - A Budget card: rename, and Archive (or Restore when archived).

**Archived budget.** All write actions are hidden and a banner says the
budget is archived and read-only; Restore lives in the Settings tab's Budget card.

**App Settings.** A new "Default currency" select, next to the time zone.

**Navigation** (`adr/0060`):
- **Desktop sidebar:**
  - Budgets is a collapsible group after the main rows, with a custom header:
    a link to `/budgets` plus a chevron toggle (a `MudNavGroup` header cannot
    be both). Its children are the active budgets in creation order.
  - Areas becomes a collapsible group the same way.
  - The collapsed state of each group is remembered per device in
    `localStorage`.
- **Phone:**
  - The bottom bar becomes Inbox, Areas, My Day, Goals, Budgets.
  - Statistics moves into `AccountDrawer`, above Settings.
  - `NavTab` maps `/budgets…` to the Budgets tab.

## 7. Testing

Every test class is tagged `[UnitTest]` or `[IntegrationTest]` (`specs/testing-spec.md`).

- **Unit, domain:**
  - Each aggregate's `Decide`/`When` for every command: happy path, every
    rejection, and archived-budget rejection.
  - Category auto-add, case-insensitive dedupe, rename/merge cascades
    emitting one event per entry, and remove-while-used rejection.
  - `StartBalanceMonth` carry-forward (rate dates kept) and the no-op start
    of a month that already has a snapshot.
- **Unit, values and reading:**
  - `Money.InPln` rounding, PLN forced to rate 1, the currency whitelist.
  - `RateSuggestion` with entries and holdings mixed.
  - `MonthTotals` and `MoneySummary`, including gaps, savings rate with zero
    income, top-4 + Other, and time-zone month boundaries.
- **Unit, components (bUnit):**
  - The entry panel's "Create category" option.
  - Rate field hidden for PLN.
  - NBP button disabled offline.
  - Archived budget hides writes.
  - Bottom bar slots and `NavTab` mapping.
  - Sidebar group collapse persisted.
- **Integration (Testcontainers MongoDB, AD-9):**
  - Commands through `/api/commands` round-trip into the four collections
    and come back through delta sync.
  - `Decimal128` storage is checked on the raw document.
  - Account wipe removes all Money documents.
  - The NBP endpoint runs against a stubbed `HttpMessageHandler`: success,
    weekend lookback, 404, timeout → 502, cache hit. Tests never touch the
    real NBP.
- **Guards.** The module-reference guard is extended: Money sees
  `Abstractions` only, and only `Api` and `App` see Money.

## 8. Delivery slices

Each slice ships green on its own:

1. Module skeleton, `Budget`, `AddCategory`, `MoneyPreferences`, sync and wipe,
   navigation changes, `/budgets`, Settings tab, default currency.
   `RenameCategory`, `MergeCategory` and `RemoveCategory` moved to slice 2:
   they cascade into entries and are untestable before entries exist.
2. `Money` (`Decimal128`), `YearMonth`, `MoneyEntry` with category auto-add, rename, merge and remove-category with their cascades, Month tab (default; tab in the URL), `EntryPanel`, `RateSuggestion`, `MonthTotals`, budget-card totals, NBP endpoint, and the `install.astro` outbound-NBP note.
3. `BalanceSnapshot`, Balance tab, `HoldingPanel`.
4. Summary tab and charts.
5. Docs: `features.astro` and the landing page.

## 9. Future: sharing a budget

Tracked in #121. It follows `adr/0054` and `adr/0057`:
- `Budget` gains `Members[]` and `InviteToken`.
- Invites use a link plus a code and expire after 30 minutes.
- `JoinBudget` is a server-only command.
- `BudgetAccess.To` grants members content writes. Rename, archive and the
  sharing commands stay owner-only.
- The sync filter widens to budgets the user is a member of.
- `ActorId` on events records who entered each entry.
- `MoneyPreferences` stays personal.

Nothing in v1 needs to change for that, beyond what §3.2 already fixes:
owner-keyed `UserId`, `BudgetId` on everything, and one access function.
