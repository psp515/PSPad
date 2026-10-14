# Budget CSV import — design

Status: Built. Extends `specs/money-design.md`; the reasoning
is in `adr/0074-budget-csv-import-runs-on-the-client.md`.

## 1. Problem and scope

People arrive with their budget history in spreadsheets. Re-typing months of
expenses, incomes and balances through the entry panels is not realistic, so
a budget needs to take that history from CSV files exported from Excel.

In:

- Importing expenses and incomes from an entries file, and monthly balances
  from a balances file, into one existing, unarchived budget, by anyone
  with content access to it: the owner or a member (`BudgetAccess.To`,
  `specs/budget-sharing-design.md`). An empty currency falls back to the
  importing user's default currency.
- A preview of every row before anything is written: errors, duplicates, new
  categories and rates fetched from NBP.
- Downloadable template files.

Out:

- Bank statement formats (MT940, bank-specific CSV) and column mapping for
  any other layout.
- Editing rows inside the preview. Errors are fixed in the spreadsheet and
  the file is picked again.
- Undoing an import as a unit. Entries are deleted one by one as today.
- Creating a budget from a file. The import runs inside an existing budget.

## 2. File format

### 2.1 Common rules

- **Encoding.** UTF-8 with or without a BOM. A file that is not valid UTF-8
  is decoded as Windows-1250, which is what Excel's plain "CSV" writes on a
  Polish Windows.
- **Delimiter.** `,` or `;`, whichever occurs in the header line outside
  quotes. A header with neither, or with both, is a file error.
- **Quoting.** RFC 4180: fields may be quoted, `""` is a literal quote, and a
  quoted field may hold the delimiter or a line break.
- **Header.** The first non-blank line. Names are trimmed and
  case-insensitive, columns come in any order, and unknown columns are
  ignored. A missing required column is a file error naming the column.
  Repeating a known column is a file error.
- **Blank lines** (every field empty) are skipped. Line numbers in messages
  are the physical line in the file, counting the header as line 1.
- **Numbers.** An optional decimal point or decimal comma. Spaces and
  non-breaking spaces are removed first, so `1 234,56` reads as 1234.56. A
  number with both `.` and `,` is an error. Amounts take at most 2 decimals,
  rates at most 6.
- **Dates.** `yyyy-MM-dd` or `dd.MM.yyyy`. **Months.** `yyyy-MM` or
  `MM.yyyy`.
- **Text** fields are trimmed before their length is checked.
- **Size.** At most 2 MB and 5,000 data rows per file; more is a file error
  asking to split the file.

### 2.2 Entries file

| Column | Required | Rule |
|---|---|---|
| `kind` | yes | `expense` or `income`, case-insensitive |
| `date` | yes | the entry's date |
| `name` | yes | 1–64 characters (`MoneyEntry.MaxNameLength`) |
| `category` | yes | 1–40 characters (`CategoryName`). A category the budget lacks is added by the command itself (§3.3 of `money-design.md`). |
| `amount` | yes | greater than 0 |
| `currency` | no | an upper-cased code from `Currencies.All`. Empty means the user's default currency. |
| `rate` | no | Ignored for PLN, which is always 1. A given rate gets `RateDate` = `date`. Empty on a foreign row means the NBP rate for `date` (§3.4). |
| `note` | no | at most 1,024 characters (`MoneyEntry.MaxNoteLength`); empty means none |

```csv
kind,date,name,category,amount,currency,rate,note
expense,2026-09-03,Biedronka,Food,84.20,PLN,,
expense,2026-09-05,Steam,PC,19.99,EUR,4.2731,
income,2026-09-10,Salary,Salary,9500.00,PLN,,September
```

### 2.3 Balances file

| Column | Required | Rule |
|---|---|---|
| `month` | yes | the balance month |
| `holding` | yes | 1–64 characters (`BalanceSnapshot.MaxHoldingNameLength`). Unique per month within the file, case-insensitive. |
| `type` | yes | `cash`, `savings` or `investment`, case-insensitive |
| `amount` | yes | greater than 0 |
| `currency` | no | as in the entries file |
| `rate` | no | Ignored for PLN. A given rate gets `RateDate` = the month's last day. Empty on a foreign row means the NBP rate for the month's last day, the date `HoldingPanel` uses. |

```csv
month,holding,type,amount,currency,rate
2026-09,Wallet,cash,350.00,PLN,
2026-09,mBank savings,savings,12000.00,PLN,
2026-09,ETF,investment,2500.00,EUR,4.2650
```

### 2.4 Templates

`wwwroot/templates/pspad-entries.csv` and `pspad-balances.csv`: the header
and the example rows above, UTF-8 with a BOM so Excel opens Polish
characters correctly.

## 3. Behaviour

### 3.1 Row status

Every parsed row gets exactly one status:

| Status | Meaning | Ticked |
|---|---|---|
| To import | valid; produces commands | yes; an entry can be unticked |
| Error | breaks a rule; the message says which | never |
| Already in budget | entries only; matches a live entry | no, can be ticked |
| Unchanged | balances only; the holding already holds these values | n/a, no command |
| Waiting for rate | an NBP rate is still being fetched | n/a; Import waits |

Balance rows are never ticked or unticked: a month is applied as a whole, so
a new month can hold exactly the file's rows.

### 3.2 Entries

- A row is **already in budget** when a live entry of the budget has the same
  kind, date, amount, currency and name (trimmed, case-insensitive).
  Identical rows inside one file are not flagged: two coffees on one day are
  real.
- **Categories.** The preview lists the categories, per kind, that the import
  will add. Existing plus new may not exceed `Budget.MaxCategoriesPerKind`
  (64). New categories are admitted in order of first appearance in the file,
  and rows using a category past the cap are errors. Category names match
  case-insensitively, and an existing category keeps its stored spelling.

### 3.3 Balances

Rows are grouped by month. The outcome of each month is computed in
ascending month order, so a month imported in the same run counts as an
earlier month for the months after it.

- **Existing month** (a live snapshot): merge by holding name,
  case-insensitive. A matching name with different type or money is
  updated, a matching name with the same values is unchanged, a new name is
  added, and holdings absent from the file stay.
- **New month** (no live snapshot): the month ends up holding **exactly** the
  file's rows. `StartBalanceMonth` copies the latest earlier month's holdings
  (`money-design.md` §3.3), so the plan then edits each copied holding whose
  name is in the file, removes each copied holding whose name is not, and
  adds the rest. Copied holdings keep their `HoldingId`, so a holding keeps
  its id across months. The preview labels the month "New month: N
  holdings" and does not show the copy-and-remove steps.
- A month's holdings after the merge may not exceed
  `BalanceSnapshot.MaxHoldings` (64); rows past the cap, in file order, are
  errors.
- Only importing builds months this way. Starting a month by hand still
  carries holdings forward.

### 3.4 Rates

- PLN rows always carry rate 1.
- A foreign row with a rate uses it as given.
- A foreign row without one needs an NBP rate for its date (entries) or its
  month's last day (balances). After parsing, the page asks
  `GET /api/money/nbp-rate` once per distinct (currency, date) pair, at most
  four requests at a time, and shows the rate and its effective date in the
  row. `RateDate` is the effective date NBP returns.
- A `404` or `502` makes those rows errors ("No NBP rate for EUR on
  2026-09-05.") and shows **Retry rates**, which asks again for the failed
  pairs only.
- Offline, those rows are errors: "Needs a rate: go online or fill the rate
  column." Going online does not refetch until **Retry rates** is pressed.

### 3.5 Rules shared with the commands

Every field rule calls the same validation the commands use (`Money`,
`CategoryName`, `Currencies.All`, the name and note limits), so a row that
passes the preview is accepted by its command, and the CSV rules cannot drift
from the domain.

## 4. Screens

### 4.1 Settings tab

The budget's Settings tab gets two cards under the category lists, one per
file: **Import entries** and **Import balances**. Each has one line of text,
a link to its template, and an **Upload entries** or **Upload balances**
button to `/budgets/{id}/import/entries` or `/budgets/{id}/import/balances`.
The owner and members both see them, since both may add entries and
balances. The cards are hidden while the budget is archived.

### 4.2 `/budgets/{id}/import/{entries|balances}` (`Pages/BudgetImportPage.razor`)

A page, not a side panel: side panels edit one thing (`adr/0052`), and the
preview is a wide table of up to 10,000 rows. The page is at most 960px wide,
like `BudgetPage`, and its heading has a back arrow to the Settings tab. An
archived or missing budget shows the archived banner, or the usual
not-found state, and no drop zone; any other last segment shows "That import
does not exist." The heading names the kind: "Import entries into Personal".
One page imports one file kind, so a year's entries and balances are two runs.

1. **Pick a file.** A line of text with the page's template link, and under
   it MudBlazor's own drag-and-drop area (`MudFileUpload DragAndDrop`): drop a
   CSV file on it or click it to open the file picker. The picked file's name
   and any file error show under it. Picking a file again replaces it and
   rebuilds the preview.
2. **Preview.**
   - A row of `MudChip`s: To import, Errors, Rates from NBP, and on the
     entries page also Already in budget and New categories.
   - New categories are listed per kind above the table.
   - Under them, the file's virtualized `MudTable` filtered by status (All,
     To import, Errors, Already in budget). A row shows its line number, its parsed values, the
     rate with its source (file, NBP with its effective date), and its status
     or error message. A checkbox ticks or unticks rows that allow it.
   - The balances table groups rows under a month header: "New month: N
     holdings" or "Existing month: updates N, adds N, unchanged N".
3. **Import.** **Import N rows** sits under the table. It is disabled while
   rates are loading and when nothing is ticked. Pressing it builds the plan
   (§5.2) and runs it with a `MudProgressLinear` showing commands done out of
   total. Commands already run stay queued in the outbox.
   - **Leaving the page.** Once a file is picked and until the import has
     finished, leaving asks first with a `ConfirmDialog`: "Leave the import?
     The file you picked won't be imported.", or while a run is going: "The
     import is still running. Leave anyway? Rows already imported stay."
     Before any file is picked, and after the result shows, leaving asks
     nothing. Reloading or closing the tab in that window gets the browser's
     own prompt.
4. **Result.** "Imported 1,240 entries and 18 holdings in 6 months." Commands
   rejected locally are listed with their line number and message, and a link
   leads back to the budget's Month tab. Later server rejections surface the
   usual way (AD-5, `adr/0021`).

The flow works offline except for NBP lookups.

## 5. Code

### 5.1 Module (`PSPad.Module.Money/Import/`)

Pure, WASM-safe and referencing `PSPad.Abstractions` only, one type per file,
namespace `PSPad.Module.Money.Import`. The module takes decoded text and
never sees bytes.

- `MoneyCsv`: `ParseEntries(string text)` and `ParseBalances(string text)`
  return a `CsvParse<TRow>`, which holds the rows, the per-row
  `CsvRowError(int Line, string Message)` and the file-level error if there
  is one. It covers §2.1–§2.3 except the currency default and NBP.
- `ParsedEntryRow(Line, Kind, Date, Name, Category, Amount, Currency?, Rate?, Note?)`
  and `ParsedHoldingRow(Line, Month, Name, Type, Amount, Currency?, Rate?)`.
- `MoneyImportPreview.Build(entries, balances, ImportContext context, RateBook rates, Func<Guid> newId)`
  applies §3.1–§3.3 and returns the per-row status, the new categories per
  kind, the per-month outcome, and the `RateRequest(Currency, Date)` pairs
  NBP must answer.
- `MoneyImportPlan.Build(preview, toggled, userId, newId)` returns
  an ordered `IReadOnlyList<ICommand>`:
  1. Months in ascending order. A new month gets `StartBalanceMonth`, then
     `EditHolding` or `RemoveHolding` for the holdings copied from the latest
     earlier month, then `AddHolding`. An existing month gets `EditHolding`
     and `AddHolding`. Unchanged holdings produce nothing.
  2. Entries in file order: `RecordExpense` or `RecordIncome`.

  Each command comes as a `PlannedCommand(ICommand Command, int? Line)` so a
  rejection can name its CSV line. `newId` is a `Func<Guid>` so tests get
  stable ids. `MoneyImportOutcome.From(plan, results)` counts what landed
  for the result line.

No command, event, aggregate or rule changes.

### 5.2 Client (`PSPad.App`)

- `Pages/BudgetImportPage.razor` and the components `ImportFilePicker`,
  `ImportEntriesTable`, `ImportBalancesTable` and `ImportSummary`.
- `ImportRates`: asks NBP for the missing pairs, four at a time, and turns
  the answers into a `RateBook`.
- Decoding: strict UTF-8 first, Windows-1250 through
  `CodePagesEncodingProvider` on failure.
- `CommandSender.SendManyAsync(IReadOnlyList<ICommand>, IProgress<int>, CancellationToken)`.
  `SendAsync` fires `Sent` and starts a sync after every accepted command,
  which across thousands of commands means thousands of re-renders and sync
  kicks. `SendManyAsync` runs each command through the same handler path,
  one at a time, collects each `CommandResult` without stopping at a
  rejection (a handler exception becomes a rejected result for that command;
  cancellation still throws), and fires `Sent` and one sync at the end, after a cancellation
  too.

### 5.3 Server

Nothing new. The outbox ships the commands through `/api/commands` in
batches of `CommandBatch.MaxEnvelopes` (50) under the per-user token bucket
(`adr/0062`), and `SyncCoordinator` already backs off on `429`.

## 6. Testing

Test-first, per `specs/testing-spec.md`.

**Unit, `PSPad.Module.Money.Tests`:**

- `MoneyCsvTests`
  - Both delimiters, BOM, quoted fields holding the delimiter, `""` and line
    breaks.
  - Decimal comma and point, spaces and NBSP as thousands separators, both
    separators in one number rejected.
  - Both date and month forms, header case and order, unknown columns
    ignored, repeated and missing columns.
  - Blank lines skipped and line numbers kept, the 5,000-row cap.
- `MoneyImportPreviewTests`
  - Each field rule gives the same message as its command.
  - Empty currency becomes the default currency, PLN ignores the rate, and a
    foreign row without a rate yields a `RateRequest` for the right date.
  - Already-in-budget matching, and identical rows within a file not
    flagged.
  - The category cap in order of first appearance, holding names repeated
    within a month, the holding cap after merging.
  - Existing month: update, add, unchanged.
  - New month: copied holdings computed across months imported in the same
    run.
- `MoneyImportPlanTests`
  - Command order, unticked and unchanged rows producing nothing.
  - `RateDate` for given and NBP rates.
  - A new month's copied holdings are edited or removed.
- `MoneyImportRoundTripTests`: the plan run through the real handlers over
  in-memory stores yields documents matching the preview. In particular a
  new month after an existing one holds exactly its file rows, with copied
  ids kept for matching names.

**Unit, `PSPad.App.Tests`:**

- `SendManyAsync` fires one `Sent` and one sync, reports progress, collects
  a rejection and goes on.
- Windows-1250 fallback decoding.
- The page:
  - Archived budget shows the banner.
  - Import is disabled while rates load.
  - Offline foreign rows without a rate are errors.

**Integration, `PSPad.Api.Tests`:** 120 import commands posted through
`/api/commands` in batches of 50 all land, and an imported new month holds
exactly its file rows on the server.

## 7. Docs

- `docs/src/pages/features.astro` and the landing page: CSV import for
  budgets.
- `specs/money-design.md` §1: CSV import moves to In, pointing here; bank
  files stay Out.
- No self-hoster change: no new env var, port or endpoint.

## 8. Delivery

Work happens in its own git worktree, `.claude/worktrees/csv-import`, on the
branch `feature/csv/import` cut from `origin/main`, so it never mixes with
other work in progress. The plan lives in `.superpowers/sdd/csv-import/`
(uncommitted). Each slice ships green on its own:

1. Spec and ADR (this document, `adr/0074`).
2. `MoneyCsv` parsing and templates.
3. `MoneyImportPreview`, then `MoneyImportPlan` and the round-trip test.
4. `CommandSender.SendManyAsync` and the decoding.
5. `BudgetImportPage`, its components and the Settings cards.
6. Integration test, `money-design.md` §1, `features.astro` and the landing
   page.
