# Investments — design

Status: Built. Issue #115. Decisions: `adr/0082` (the module), `adr/0083`
(`Money` and `Currencies` move to Abstractions), `adr/0084` (Yahoo quotes),
`adr/0085` (a holding links to an investment account), `adr/0086` (phone
bottom bar). UI mockups: "PSPad Investments UI"
(https://claude.ai/artifact/4yhrKTbTaLfgYVvepAdqro).

## 1. Problem and scope

A budget's balance holds one number per investment holding, typed by hand.
The user keeps several brokerage accounts (XTB: an IKE in PLN, plus accounts
in USD and EUR), wants to see what each position is worth, what was earned
and lost, and wants that value in the budget's balance on a chosen day
without retyping it.

In:

- Investment accounts, each in one currency, private to their owner.
- Operations: deposits, withdrawals, buys, sells, dividends, withholding tax,
  interest and fees. Imported from XTB's xlsx export, incrementally and
  without duplicates, or entered by hand.
- Creating an account straight from an XTB file.
- Daily closing prices from Yahoo Finance, fetched by the server at most once
  a day per symbol, and cached on the client for offline use.
- Positions with their value and profit, per account and across all
  accounts in PLN.
- A summary: total profit, realized gains and realized losses kept apart,
  unrealized profit, passive income, fees, and the currency difference.
- A price chart per instrument with buy, sell and dividend markers, and a
  value-over-time chart against net deposits.
- Copying accounts into a budget's balance on a chosen day, as one
  Investment holding per account, with prices refreshed first.

Out:

- Brokers other than XTB for import. Other brokers are entered by hand.
- Stock splits, spin-offs, ticker changes, options, CFDs, crypto.
- Intraday charts, live prices, alerts.
- Tax reports (PIT-38).
- Sharing an account. Accounts are private; only the budget copy is shared.
- Live links: a linked holding is a copy, refreshed on demand only.

## 2. Module

`src/modules/PSPad.Module.Investments`. It references `PSPad.Abstractions`
only, so it compiles to WASM and its handlers run in the browser and on the
server, like Tasks and Money (AD-3, AD-4, `adr/0082`). No module references
it; only `PSPad.Api` and `PSPad.App` do. It knows nothing about Money: the
budget link is a `Guid` on the holding (§8), and the value is computed in
`PSPad.App`, which sees both modules.

`Money` and `Currencies` move from `PSPad.Module.Money` to
`PSPad.Abstractions`, keeping their namespaces (`PSPad.Module.Money` and
`PSPad.Module.Money.Values`), so no call site, contract or stored document
changes (`adr/0083`).

```
Portfolio/      InvestmentPortfolio.cs, PortfolioAccountAdded.cs, PortfolioTickerAdded.cs
Accounts/       InvestmentAccount.cs, Broker.cs, InvestmentAccountAccess.cs,
                Create/, Rename/, Archive/, Restore/, SetBrokerAccountNumber/
Operations/     InvestmentOperation.cs, OperationKind.cs, OperationSource.cs,
                OperationValues.cs, Record/, Edit/, Delete/, Restore/
Instruments/    Instrument.cs, QuoteSymbols.cs, Ensure/, SetQuoteSymbol/
Import/         Xlsx/ (XlsxReader, XlsxBook, XlsxSheet, XlsxCell), ExcelTime.cs,
                XtbParser.cs, XtbFile.cs, XtbFileError.cs, XtbRow.cs, XtbRowKind.cs,
                XtbComment.cs, XtbTypeMap.cs, XtbOpenPosition.cs, OpenPositionCheck.cs,
                OpenPositionDifference.cs, ImportProbe.cs, ImportCommands.cs,
                ImportRateKey.cs, ImportRowPreview.cs, ImportRowStatus.cs,
                InvestmentImportPreviewer.cs, InvestmentImportPreview.cs,
                NewAccountProposal.cs, InvestmentImportPlan.cs
Reading/        LiveOperations.cs, Lots.cs, LotBook.cs, Lot.cs, ClosedResult.cs,
                Position.cs, PositionWarning.cs, Valuation.cs, AccountFigures.cs,
                PortfolioFigures.cs, PriceHistory.cs, DailyClose.cs, FxBook.cs,
                DailyRate.cs, Dated.cs, ValueOverTime.cs, ValuePoint.cs,
                AccountValueOnDay.cs, PriceMarkers.cs, PriceMarker.cs, MarkerKind.cs
DeterministicId.cs, InvestmentsModuleMarker.cs
```

Grouping operations by the user's day (`OperationDays`) lives in
`PSPad.App/State/Investments`, next to the page that shows it.

## 3. Domain model

### 3.1 Aggregates

| Aggregate | Id | Fields | Collection |
|---|---|---|---|
| `InvestmentPortfolio` | `IdFor(userId)` | `UserId`, `AccountIds[]`, `Tickers[]` | `investmentportfolios` |
| `InvestmentAccount` | Guid | `UserId`, `Name`, `Currency`, `Broker` (Xtb, Other), `BrokerAccountNumber?`, `OperationCount`, `CreatedAt`, `ArchivedAt?` | `investmentaccounts` |
| `InvestmentOperation` | Guid (§3.3) | `UserId`, `AccountId`, `Kind`, `At` (UTC instant), `Ticker?`, `Quantity?`, `Price?`, `Money`, `PositionId?`, `Source` (Manual, Xtb), `ExternalId?`, `Note?`, `RecordedAt`, `Deleted` | `investmentoperations` |
| `Instrument` | `IdFor(userId, ticker)` | `UserId`, `Ticker`, `Name`, `QuoteSymbol`, `UpdatedAt` | `instruments` |

- `Money` is the operation's cash effect in the **account's** currency, with
  the NBP rate to PLN of the operation's day frozen in it (AD-14). `Amount`
  is always more than 0; `Kind` gives the sign.
- `Quantity` and `Price` are in the instrument's units and currency. They
  are required on Buy and Sell, and absent elsewhere. `Price` is what the
  broker reported. It feeds chart markers only, never a sum: the `Money`
  amount is the truth.
- `Ticker` is the broker's ticker (`ABC.US`, `DEF.PL`). It is required on
  Buy, Sell, Dividend and Tax, and absent on the rest.
- `PositionId` is XTB's position id. Buys sharing one form one lot; a sell
  with one closes that lot (§5.1).
- An `Instrument` holds the per-user mapping from ticker to Yahoo symbol and
  a display name. Its currency is not stored: it comes with the price series
  (§6).
- `InvestmentPortfolio` is one document per user. It exists to count: it
  lists the user's accounts and instrument tickers, so the per-user caps
  (§3.2) are checked inside one aggregate. It is created by the user's first
  `CreateInvestmentAccount` or `EnsureInstrument`.
- `OperationCount` counts every operation ever recorded in the account,
  deleted ones included, so the per-account cap (§3.2) is checked inside the
  account.
- `UserId` is the owner on every document. Accounts are private:
  `InvestmentAccountAccess.Owned(account, actorId)` grants the owner only,
  and `Writable` adds the archived check.

### 3.2 Values and limits

| Limit | Value | Applies to |
|---|---|---|
| `InvestmentAccount.MaxNameLength` | 64 (`ContentLimits.Name`) | account name |
| `InvestmentAccount.MaxBrokerAccountNumberLength` | 32 | broker account number |
| `InvestmentOperation.MaxNoteLength` | 1,024 (`ContentLimits.FieldValue`) | note |
| `Instrument.MaxTickerLength` | 24 | ticker |
| `Instrument.MaxQuoteSymbolLength` | 24 | Yahoo symbol |
| `Instrument.MaxNameLength` | 64 | instrument name |
| `InvestmentOperation.MaxQuantity` | 1 000 000 000, at most 8 decimals | quantity |
| `InvestmentOperation.MaxPrice` | 1 000 000 000, at most 8 decimals | price |
| `InvestmentOperation.MaxPositionIdLength` | 32 | broker position id |
| `InvestmentPortfolio.MaxAccounts` | 32, archived included | `CreateInvestmentAccount` |
| `InvestmentPortfolio.MaxInstruments` | 500 | `EnsureInstrument` |
| `InvestmentAccount.MaxOperations` | 20,000, deleted included | `RecordInvestmentOperation` |

Lengths count after trimming, as in `specs/content-limits-design.md`. Over a
limit is a `DomainRejectedException`, surfaced like any rejection. Counts are
checked inside one aggregate, as every other count limit is: the portfolio
for accounts and instruments, the account for its operations. The handler
stages both documents in one unit of work, the cascade pattern of
`adr/0042`. On the server the handler reloads both, so two offline devices
cannot pass a cap together.

### 3.3 Commands and rules

**Accounts**

- `CreateInvestmentAccount(accountId, name, currency, broker,
  brokerAccountNumber?)`. The currency comes from `Currencies.All` and never
  changes. It emits `InvestmentAccountCreated` on the account and
  `PortfolioAccountAdded` on the portfolio; the 33rd account is rejected:
  "You can have at most 32 investment accounts."
- `RenameInvestmentAccount`, `ArchiveInvestmentAccount` and
  `RestoreInvestmentAccount` work like their budget counterparts: archiving
  twice is a no-op, and an archived account rejects every write except
  restore ("This account is archived.").
- `SetBrokerAccountNumber` sets the number when it is empty. Changing a set
  number is rejected; it guards the import (§4.3).
- There is no delete.

**Operations**

- `RecordInvestmentOperation(operationId, accountId, kind, at, ticker?,
  quantity?, price?, money, positionId?, source, externalId?, note?)`.
  - The money's currency must be the account's ("An operation is in the
    account's currency.").
  - A manual operation has no external id; an imported one has one.
  - Recording an id that already exists, live or deleted, is a no-op. That
    makes a repeated import harmless, even from two offline devices.
  - It also emits `AccountOperationCounted` on the account. The 20,001st
    operation is rejected: "An account can hold at most 20,000 operations."
  - An imported operation's id is `IdFor(accountId, externalId)`: the
    account id XORed with a fixed salt and the numeric XTB row id, the
    pattern `BalanceSnapshot.IdFor` uses, so every device derives the same
    one. (The module may not use `System.Security.Cryptography`, so no
    name-based UUID.) An XTB id must be digits that fit a `long`; anything
    else is a row error. A manual operation's id is random.
- `EditInvestmentOperation` replaces kind, time, ticker, quantity, price,
  money, position id and note. `Source` and `ExternalId` never change, so an
  edited import row stays recognised on the next import.
- `DeleteInvestmentOperation` is a soft delete. `RestoreInvestmentOperation`
  undeletes it.
- Selling more than the account holds is **not** rejected. It depends on
  other documents, and an offline device may not have them all yet. The
  reading layer flags it (§5.1).

**Instruments**

- `EnsureInstrument(ticker, name)` creates the instrument with the default
  symbol (§6.3), or does nothing when it exists. Recording or importing an
  operation with a new ticker sends it first. A new instrument also emits
  `PortfolioTickerAdded`; the 501st is rejected: "You can have at most 500
  instruments."
- `SetQuoteSymbol(ticker, symbol)` changes the Yahoo symbol: the "Fix
  symbol" action on a position that has no prices.

### 3.4 Events

`PortfolioAccountAdded`, `PortfolioTickerAdded`,
`InvestmentAccountCreated`, `InvestmentAccountRenamed`, `AccountOperationCounted`,
`InvestmentAccountArchived`, `InvestmentAccountRestored`,
`BrokerAccountNumberSet`, `InvestmentOperationRecorded`,
`InvestmentOperationEdited`, `InvestmentOperationDeleted`,
`InvestmentOperationRestored`, `InstrumentEnsured`, `QuoteSymbolSet`. Each
carries the full values its fold needs (`adr/0037`).

## 4. XTB import

The import runs on the client as ordinary commands, as the budget CSV import
does (`adr/0074`). It adds no endpoint except the quote and rate lookups
both features already use.

### 4.1 File

- An `.xlsx` of at most 5 MB, read in the browser: the file is a ZIP of
  XML parts. `System.IO.Compression` and `System.Xml` both run in WASM. The
  reader understands shared strings, inline strings and numbers. Styles,
  formulas and dates as styles are ignored; times are Excel serials.
- Sheets are found by their header row, not their names, because XTB
  localises sheet names. The **Cash Operations** sheet is the one whose
  header holds `Type`, `Instrument`, `Ticker`, `Time`, `Amount`, `ID`,
  `Comment`, `Product` and `Position ID`.
- The account number is the `Account number` cell above the header of
  Cash Operations. Other sheets are not used for it: in a real export the
  Open Positions sheet carried a different number.
- At most 20,000 data rows. The `Total` row and blank rows are skipped.
- A file without a Cash Operations sheet, or without an account number, is
  a file error.

### 4.2 Rows

| XTB `Type` | Kind | Notes |
|---|---|---|
| `Stock purchase` | Buy | quantity and price from the comment |
| `Stock sell` | Sell | quantity and price from the comment |
| `Deposit`, `IKE deposit`, `IKZE deposit` | Deposit | |
| `Withdrawal` | Withdrawal | |
| `Dividend` | Dividend | |
| `Withholding tax` | Tax | |
| `Free funds interest` | Interest | |
| `Commission`, `SEC fee` | Fee | |
| anything else | — | row error naming the type |

- **Amount.** The absolute value of `Amount`, in the account currency.
  Its sign must match the kind (a purchase is negative, a sell positive);
  a mismatch is a row error. Zero is a row error.
- **Time.** `Time` is an Excel serial in UTC: days since 1899-12-30.
- **Comment.** `OPEN BUY <ticker> <quantity> @ <price>` on a purchase,
  `CLOSE BUY <ticker> <quantity> @ <price>` on a sell. The quantity may have
  a fraction (`2.2`). An unparsable comment on a buy or sell is a row error.
- **Ticker and name** come from `Ticker` and `Instrument`.
- **External id** is `ID`; a row without one is a row error.
- **Position id** is `Position ID`; a buy or sell without one is a row
  error.
- **Rate.** A PLN account needs none. Otherwise the NBP rate for the
  operation's day, fetched once per currency over the file's date range
  (§7.2). The day is the date of `Time` in the user's stored time zone.

### 4.3 Target account

- From an account's Import action, the file's number must equal the
  account's `BrokerAccountNumber`. An account without one takes the file's
  (`SetBrokerAccountNumber` is part of the import), unless another account
  already holds that number: then it is a file error, "This file belongs to
  account {name}.", and no number is set. A different number is a
  file error: "This file is from account 12345678."
- From the Investments page's "Import from XTB", the number picks the
  account. An unknown number offers a new account in the preview: the name
  defaults to the `Product` value and the number ("IKE 12345678"), and the
  user picks the currency, which the file does not state. Confirming sends
  `CreateInvestmentAccount` before the operations.
- Four XTB accounts are four files and four PSPad accounts.

### 4.4 Preview

| Status | Meaning | Ticked |
|---|---|---|
| To import | valid, id unknown | yes, can be unticked |
| Already imported | id exists and is live | n/a |
| Deleted earlier | id exists and is deleted | no, can be ticked: sends `RestoreInvestmentOperation` |
| Error | breaks a rule; the message says which | never |
| Waiting for rate | an NBP rate is still being fetched | n/a; Import waits |

**Wrong rows are shown, never hidden.** The preview lists every Error row
first, each with its row number in the sheet, the values XTB gave (type,
time, ticker, amount, ID, comment) and the reason, for example "Row 41:
unknown type 'Stock split'" or "Row 87: the comment does not say how many
shares". A switch shows errors only. Errors never block the other rows:
Import sends the ticked rows and the error rows stay out. Fixing them means
entering the operation by hand or importing a corrected file later; a
re-import skips what already came in.

**Limits in the preview.** When the ticked rows would take the account past
20,000 operations, or the new tickers past 500 instruments, the preview says
so ("This import needs 1,240 more operations than the account can take")
and Import stays disabled until rows are unticked. A file that would create
a 33rd account offers no new account.

The preview also shows the counts per kind, the new instruments, and the
**check**: the open lots the import would leave (§5.1) are compared with the
file's Open Positions sheet, when it has one, by position id and volume.
Matching shows "Open positions match the file". Differences are listed and
do not block the import: the sheet is a snapshot from the export time, and
operations entered by hand may explain them.

### 4.5 Confirm

Commands in order: `CreateInvestmentAccount` or `SetBrokerAccountNumber`
when needed, `EnsureInstrument` per new ticker, then
`RecordInvestmentOperation` per ticked row, oldest first, sent with
`CommandSender.SendManyAsync`. The outbox ships them like any other commands
(`adr/0074` consequences apply).

## 5. Reading (pure, in the module, shared by client and server)

All figures for one account are in the account's currency; the PLN totals
across accounts are in §5.3.

### 5.1 Lots and positions

- **Lots.** Buys with the same `PositionId` form one lot: quantity and cost
  (the buys' amounts) add up. A manual buy without a position id is its own
  lot.
- **Closing.** A sell with a `PositionId` closes that lot. A sell without
  one (manual) closes the ticker's lots oldest first (FIFO). A partial close
  takes cost pro rata to quantity.
- **Realized result** of a sell = its amount − the cost it closed. A
  positive result counts as a realized gain, a negative one as a realized
  loss; the two are never netted.
- **Oversold.** A sell needing more than the lot or ticker holds closes what
  there is, and the position carries an "Oversold by n" warning. The import
  check (§4.4) shows the same.
- **Position** per ticker: open quantity, open cost, average price (open
  cost ÷ quantity), last close, value (quantity × last close, converted to
  the account currency), unrealized result (value − open cost), the day the
  price is from, and the lots.
- **No price.** An instrument with no price series yet is valued at cost,
  unrealized 0, and marked "No price". Its row offers "Fix symbol".

### 5.2 Account figures

- Net deposits = deposits − withdrawals.
- Cash = Σ signed amounts of every operation.
- Positions value = Σ position values. Account value = cash + positions
  value.
- Total profit = account value − net deposits.
- Realized gains, realized losses, unrealized (Σ positions), passive income
  (dividends − tax + interest), fees.
- **Identity:** total profit = realized gains + realized losses + unrealized
  + passive income − fees. It holds exactly in the account currency, and a
  test asserts it on every fixture.

### 5.3 Across accounts (PLN)

- Values are converted at today's NBP rate; net deposits, realized,
  passive income and fees at each operation's frozen rate; unrealized at
  today's rate.
- Currency difference = total profit in PLN − (the components in PLN). It
  is what exchange rates added or took since the money went in, and it is
  shown as its own line, so the PLN identity holds too.
- PLN accounts have no currency difference.

### 5.4 Converting prices

A price is in the instrument's currency; the account may be in another.
Conversion goes through PLN with NBP rates of the same day: price ×
rate(instrument) ÷ rate(account). Yahoo's `GBp` and `ILA` are minor units
and are divided by 100 first.

### 5.5 Value over time

- One point per day from the first operation to today. With more than a
  year shown, one point per week (the week's last day).
- Each point: cash plus open lots valued at the close on or before that day,
  converted with the NBP rates on or before that day, and the net deposits
  that day.
- Before an instrument's first close, its lots count at cost.
- Range chips: 3M, 1Y, All. "All accounts" sums the accounts in PLN at each
  day's rate.

### 5.6 Value on a day

`AccountValueOnDay.For(account, operations, prices, fx, day)` is §5.5's
point for one day, plus the day each instrument's price is from. The budget
link (§8) uses it.

## 6. Quotes

### 6.1 Endpoint

`GET /api/investments/quotes?symbol=ABC&from=2024-01-02&refresh=false`

- Authenticated, not a command, server-only in `PSPad.Api/Quotes`, built
  like the NBP client (`adr/0084`).
- Returns `{ symbol, currency, closes: [{ date, close }], latest: { at,
  price }?, fetchedAt }`, closes from `from` (or the earliest cached) to
  today. `latest` is the most recent market price when Yahoo reports one.
- Calls `https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?period1=…&period2=…&interval=1d`
  with a 10 s timeout.
- Draws on a per-user `Quotes` token bucket (burst 100, 1/s, backend-spec
  rate limits); an empty bucket answers `429` and the client keeps its stored
  series.

### 6.2 Cache

- A Mongo collection `quotes`, one document per symbol, shared by all users
  of the instance: it holds public prices, not user data. It is not synced
  and not wiped with an account.
- A request is served from the cache when its range is covered and it was
  fetched today (UTC). Otherwise the server fetches from the earlier of
  `from` and the cached start, to today, and replaces the document.
- `refresh=true` refetches when the cached copy is more than 15 minutes
  old. The budget link (§8) and the page's refresh button send it.
- Unknown symbol (Yahoo 404, or a response with no result) gives `404`.
  Yahoo 5xx, 429, timeout, network error or malformed JSON gives `502`,
  unless the cache has the symbol: then the cached copy is returned with
  `stale: true`.

### 6.3 Symbols

The default Yahoo symbol from an XTB ticker: `.PL` → `.WA`, `.UK` → `.L`,
`.US` → no suffix, `.DE` → `.DE`, `.FR` → `.PA`, `.NL` → `.AS`, other
suffixes kept as they are. `SetQuoteSymbol` overrides it per user.

### 6.4 Client

- The client keeps the last response per symbol in an IndexedDB store
  `quotes`, beside the replica. It is a cache of public data, never synced,
  cleared on sign-out with the rest.
- Opening Investments requests every held symbol not fetched today, a few at
  a time. Offline, the page uses what is stored and shows "Prices from
  27 Oct".

## 7. Rates

### 7.1 Single day

The existing `GET /api/money/nbp-rate` gives the rate on or before a day.

### 7.2 Series

`GET /api/money/nbp-rates?currency=USD&from=2024-01-02&to=2026-10-30`, new
in `PSPad.Api/Rates`, returns every published rate in the range. NBP serves
at most 93 days per call, so the server splits the range and caches each
chunk like single rates. The import uses it for frozen rates, and §5.4–5.5
use it for conversion. The client caches series in the `quotes` store under
`fx:USD`.

## 8. Budget link

### 8.1 Holding

`Holding` gains `InvestmentAccountId?` (a `Guid`, `adr/0085`). `AddHolding`
and `EditHolding` carry it, `HoldingAdded` and `HoldingEdited` record it,
and `StartBalanceMonth` copies it with the rest. Money validates nothing
about it. A month holds at most one holding per linked account ("That
account is already in this month.").

### 8.2 Add from investments

On a budget's Balance tab: a button beside "Add holding" on desktop, a row
in the + menu on phones. It opens a side panel:

1. **Day.** Defaults to today when the month is the current one, else the
   month's last day. It must lie in the month and not after today.
2. **Accounts.** The user's own unarchived accounts, all ticked. An account
   already linked in this month shows "Update" instead of "Add".
3. **Prices.** The panel requests every held symbol of the ticked accounts
   with `refresh=true`, and shows "Updating prices…". Each account then shows
   its value and the day its prices are from ("Prices from 30 Oct, 16:05").
   - Yahoo or the network down: the last cached close, marked "Prices from
     27 Oct".
   - An instrument never priced: valued at cost, marked "No price".
4. **Rate.** A PLN account needs none. Otherwise the NBP rate on or before
   the day, frozen in the holding. Offline it falls back to
   `RateSuggestion`, as `HoldingPanel` does, and the field stays editable.
5. **Confirm** sends `AddHolding` (name = account name, type Investment,
   money = value in the account currency with that rate,
   `InvestmentAccountId` set) or `EditHolding` for an already linked
   account. An account worth 0 or less is skipped with "Nothing to add".

The month needs a snapshot first; the panel sends `StartBalanceMonth` when
it has none, as the holding panel does.

### 8.3 Refresh from investments

A month with linked holdings shows "Refresh from investments" in its header.
It opens the same panel with only the linked accounts ticked; confirming
sends `EditHolding` for each. The refreshed prices also update the client's
`quotes` store, so the Investments page shows the same values afterwards.

### 8.4 Shared budgets and editing

- In a shared budget, a linked holding is an ordinary holding to members.
  Only the account's owner sees it in the panel or can refresh it.
- A linked holding shows a link icon. For the owner, the row's menu offers
  "Open account" and "Unlink" (`EditHolding` with no id).
- Editing a linked holding by hand keeps the link; the next refresh replaces
  the value.

## 9. Screens

The mockups are the layout; the look is the app's (MudBlazor, the existing
`pspad-*` classes, ui-spec). Edits happen in side panels (`adr/0052`), one
FAB per page (`adr/0033`).

### 9.1 Navigation (`adr/0086`)

- **Phone bottom bar:** Inbox, Areas, My Day (raised), Budgets, Portfolio.
  "Investments" does not fit at 390 px, so the slot's label is "Portfolio";
  the page title stays "Investments". Goals moves to `AccountDrawer`, as the
  first row above Statistics.
- **Desktop sidebar:** an Investments group after Budgets, built like it: a
  link to `/investments`, a chevron, and the unarchived accounts with their
  total profit.
- Routes: `/investments` (all accounts), `/investments/{accountId}`,
  `/investments/import`.

### 9.2 Investments page

- **Header** like the budget page. **Account chips** below it: "All" and
  one per unarchived account; the route follows the chip.
- **Tabs:** Positions, Operations, Accounts.
- **Positions:**
  - `MoneyHero` with the account value and total profit, then a stat row:
    realized gains, realized losses, unrealized, passive income, fees,
    currency difference (PLN view only).
  - Value-over-time chart (`MudChart` line): value and net deposits, with
    the range chips.
  - Positions grouped by account, each row in the `pspad-holding-row`
    layout: ticker and name, quantity × average price, value, unrealized in
    amount and percent, price date, warnings.
  - An expanded row shows the price chart (custom SVG: the close line, green
    buy, red sell and blue dividend markers with a tooltip naming the
    operation) and the open lots.
- **Operations:** grouped by day, newest first, filtered by kind; each row
  opens the operation panel. The FAB adds an operation.
- **Accounts:** a card per account with currency, broker, number, value and
  profit, and Rename, Archive, Import actions. Archived accounts are behind
  a "Show archived" switch.

### 9.3 Panels

- **Operation panel.** Kind first; the fields follow it (§3.1). Time
  defaults to now. Money is the amount only; its currency is the account's,
  and its rate is fetched from NBP for the day, editable, as in the entry
  panel.
- **Account panel.** Name, currency (fixed after creation), broker, number.
- **Import page.** Pick file → preview (§4.4) → Import. Progress counts the
  commands handed to the local outbox; when they are all in, the page goes
  back to Investments. What is still waiting for the server shows on the
  outbox chip, like any other offline write.

## 10. Sync, storage and API

- `investmentportfolios`, `investmentaccounts`, `investmentoperations` and `instruments` join delta
  sync by `seq` (AD-6), filtered by `UserId`, through `SyncReader`,
  `SyncService`'s collection map and `MongoIndexes`. Existing replicas pull
  them from zero once (`adr/0049`). The replica keeps them in its generic
  `documents` store.
- `investmentportfolios` syncs with them.
- `investmentoperations` gets an index on `(UserId, AccountId)`.
- Commands go through `/api/commands` and the outbox; the handlers are
  registered in both hosts.
- `UserDataWipe` wipes the four collections; `quotes` is not user data.
- **Self-hosting.** The API container needs outbound HTTPS to
  `query1.finance.yahoo.com` for prices, as it needs `api.nbp.pl` for rates.
  Without it, investments work with cost values and "No price". No new env
  var or port.

## 11. Testing

Unit tests (`[UnitTest]`), behaviour only (`adr/0080`):

- Each command's rules: currency match, archived account, no-op on a known
  id, deleted ids, edits keeping source and external id, length limits, and
  the count caps (32 accounts, 500 instruments, 20,000 operations, deleted
  and archived included).
- Lots: position-id lots, partial closes, FIFO for manual sells, oversold.
- Account figures on fixtures, with the identity asserted on each; PLN
  totals and the currency difference.
- Value over time: daily and weekly points, cost before the first close,
  conversion through PLN, `GBp`.
- XTB import on synthetic xlsx files built in the test: sheet found by
  header, account number above the header, type map, comment parsing with
  fractions, Excel serials, sign mismatches, the statuses, re-import, the
  account-number guard, account creation, the Open Positions check, error
  rows listed with row number, values and reason while the valid rows still
  import, and the cap messages. The
  user's real export never enters the repository.
- Symbol mapping.
- Budget link: value on a day, stale and missing prices, one holding per
  account, `StartBalanceMonth` carrying the id.

Integration tests (`[IntegrationTest]`): the quote endpoint against a fake
Yahoo handler (cache hit, daily refetch, 15-minute refresh, stale on
failure, 404 and 502), the NBP series chunking, sync of the new collections,
and `UserDataWipe`.

E2E: import a synthetic file, see the positions, add the account to a budget
from the Balance tab.

## 12. Delivery slices

1. `Money` and `Currencies` move; module, accounts, operations, instruments,
   sync; Operations and Accounts tabs with manual entry.
2. XTB import.
3. Quotes and series endpoints, client cache, Positions tab, figures and
   charts.
4. Budget link and the phone bottom bar.
5. Docs: `self-host.astro`, `docker/.env.example`, `features.astro`, landing
   page.
