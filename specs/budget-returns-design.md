# Budget Returns Design

A purchase that goes back to the shop. The expense stays on record as spent;
a separate return entry, linked to it, gives money back on the day it
happens. Decision: `adr/0090`. Mockups: the "Returns" row of the "PSPad
Budgets" design canvas (https://claude.ai/artifact/Qfud3qPJCZfVLjbCqgJk42;
layout only, styling follows the app, `specs/ui-spec.md`). Builds on
`specs/money-design.md` and `specs/budget-overview-design.md`.

## 1. Scope

In:

- Record, edit and delete a return against one expense, full or partial,
  any number of times up to the purchase amount.
- A return lowers expenses in the month of its own date, in the purchase's
  category, at the purchase's frozen rate.
- The purchase shows how much of it was returned; the return shows what it
  returns.

Out:

- Returns against income.
- Returns without a source purchase (a refund for something never
  recorded): record it as income, or edit the purchase.
- Returns in CSV import.

## 2. Domain (`PSPad.Module.Money/Entries`)

### 2.1 Model

`MoneyEntry` gains `ReturnOf: Guid?`. A return is a `MoneyEntry` with
`Kind = Expense` and `ReturnOf` set to its purchase's id. Its `Name`,
`Category` and `Money.Currency`, `Money.RateToPln`, `Money.RateDate` are
the purchase's; `Money.Amount` (still `> 0`), `Date` and `Note` are its own.

- `IsReturn => ReturnOf is not null`.
- `SignedPln`: `Money.InPln` for a purchase or an income, `-Money.InPln` for
  a return. Every reader that sums expenses sums `SignedPln` (§3).

A purchase is a live `Expense` entry with no `ReturnOf`.

### 2.2 `ReturnState`

`ReturnState.Of(purchase, entries)` (in `Entries/`, pure): the purchase's live
returns, newest date first; `Returned` (sum of their `Money.Amount`) and
`Left` (`purchase.Money.Amount - Returned`), both in the purchase's currency;
`IsFull => Left == 0`. Handlers, the panels and the rows all use it.

### 2.3 Commands

Folders `Entries/RecordReturn/` and `Entries/EditReturn/`.

**`RecordReturn(CommandId, UserId, EntryId, PurchaseId, Amount, Date, Note?)`**

The handler loads the purchase, its budget and the owner's entries
(`LoadAllAsync(budget.UserId)`, as `RenameCategoryHandler` does).
`MoneyEntry.Decide` rejects, in order:

1. Missing or deleted purchase, or a purchase in a budget the actor can't
   reach: "That entry does not exist." (unrecoverable).
2. Archived budget: "This budget is archived."
3. An entry id already taken (`RequireNew`).
4. Purchase is income or itself a return: "Only an expense can be returned."
5. `Amount <= 0`: "Enter an amount."
6. `Date < purchase.Date`: "A return can't be dated before its purchase."
7. `Amount > Left`: "That is more than is left to return."
8. Note longer than `MaxNoteLength` (the existing note rule).

It emits `ReturnRecorded(EntryId, UserId (budget owner), At, BudgetId,
PurchaseId, Name, Category, Money, Date, Note)` with the purchase's name,
category and rate, and the command's amount. No category is ever added.

**`EditReturn(CommandId, UserId, EntryId, Amount, Date, Note?)`**

Loads the return, its purchase, the budget and the owner's entries. Rejects a
missing return or one that isn't a return ("That entry does not exist.") and
a missing purchase the same way; then rules 2, 5, 6, 8, and rule 7 with
`Left` computed without this return. No change emits nothing; otherwise
`ReturnEdited(EntryId, UserId, At, Money, Date, Note)`.

**`EditEntry`** on a return is rejected: "Edit the return instead."
On a purchase with live returns it is rejected when:

- the amount is below `Returned`: "The amount can't be less than what was
  returned.";
- the currency or rate changes: "A purchase with returns keeps its currency
  and rate.";
- the date moves after the earliest return: "A purchase can't be dated after
  its returns.".

A category or name change on such a purchase emits `MoneyEntryEdited` for
the purchase and, in the same unit of work, `MoneyEntryRelabelled(Name,
Category)` for each live return that differs. `EditEntryHandler` therefore
loads the owner's entries too. `MoneyEntryCascade` grows the step that stages
them.

**`DeleteEntry`** on a purchase also emits `MoneyEntryDeleted` for each live
return, in the same unit of work. On a return it deletes only the return.
`DeleteEntryHandler` loads the owner's entries.

**Category cascades.** `RenameCategory` and `MergeCategory` already emit
`MoneyEntryRecategorised` for every live entry of the kind; returns are
expense entries, so they follow. `RemoveCategory` counts returns as uses.

### 2.4 Events

New: `ReturnRecorded`, `ReturnEdited`, `MoneyEntryRelabelled`. Each carries
the full values its fold needs (`adr/0037`). `MoneyEntry.Apply` folds them;
`ReturnRecorded` sets `ReturnOf`.

### 2.5 Offline

Two devices recording returns against one purchase offline can together
exceed it. Each device's own check passes; the second to reach the server is
rejected by rule 7 and surfaced like any rejected command (AD-5). Device A
deleting the purchase while B records a return offline: B's command is
rejected with "That entry does not exist." when it syncs.

## 3. Reading (`PSPad.Module.Money/Reading`)

Every expense sum uses `SignedPln` instead of `Money.InPln`:

- `MonthTotals`: expenses, the per-category totals (a category can be
  negative), and `EntryDay.Net` (a return adds back).
- `CategoryTotal.RankingOf`: sums `SignedPln`; order unchanged (largest
  first), so a negative category sorts last.
- `MoneySummary`: expenses, saved, savings rate, `PerMonth`, the category
  series and ranking.
- `UsualMonth`, `MonthPace`, `Insights`: through the same sums. A month with
  only returns still counts as a sampled month.
- `MonthTotals` gains `Returned`: the PLN sum of returns in the month (shown
  under Expenses).
- `RateSuggestion` ignores returns: their rate is the purchase's, not a new
  observation.

Balance and investments are untouched. Statistics doesn't read Money.

## 4. Screens (`PSPad.App`)

### 4.1 Rows (`EntryRow`)

- A return: a `MudIcon` `Icons.Material.Outlined.Undo` before the name; the
  caption reads "Return · {category}" (plus the foreign amount as today); the
  amount is `+{InPln}` in the default color.
- A purchase with live returns: a small outlined `MudChip` after the caption,
  "Returned {Returned}" (in the purchase's currency text) or "Returned" when
  full; a full return also strikes through the amount
  (`pspad-entry-amount-returned`).
- Tapping a return opens `ReturnPanel`.

### 4.2 `EntryPanel` (editing a live purchase, budget not archived)

Below the note a "Returns" section (`pspad-entry-returns`):

- "Returned {Returned} of {Amount}" and a `MudProgressLinear`, shown only
  when returns exist.
- One `MudListItem` per return (date, `+amount`), opening `ReturnPanel`.
- "{Left} left to return" and an outlined "Record return" button, disabled
  when `IsFull`. It opens `ReturnPanel` for a new return.
- With returns: currency and rate are read-only, the helper text under the
  amount reads "Has returns: currency is locked and the amount can't go
  below {Returned}.", and the delete confirmation names the returns that go
  with it.

Income panels and new entries show no Returns section.

### 4.3 `ReturnPanel` (`Layout/ReturnPanel.razor`)

A `DetailPanel` like `EntryPanel`.

- Header card: purchase name, category chip, bought date, paid (amount,
  currency, PLN at rate), returned so far, left to return, and a "Go to
  purchase" link to `?entry={purchaseId}`.
- Amount (`MudNumericField`, adornment = the purchase's currency),
  pre-filled with `Left` for a new return; a read-only "In PLN".
- Helper: "Uses the purchase's rate, so a full return cancels it exactly."
- Date (`MudDatePicker`), default today in the user's time zone,
  `MinDate` = purchase date; helper "Counts in {Month yyyy}."
- Note.
- Save, Cancel, Delete when editing. Read-only when the budget is archived.
- `CanSave`: `0 < amount <= Left (+ own amount when editing)`, date set.

### 4.4 Routing (`EntryQuery`)

- `?return={returnId}` opens `ReturnPanel` on an existing return.
- `?returnof={purchaseId}` opens it for a new return.
- `?entry={id}` on a return id redirects to `?return={id}`.
- `AppShell` hosts `ReturnPanel` next to `EntryPanel`; only one is open.

### 4.5 Filters and totals

- `EntryFilters`: Expenses includes returns; category filter and search
  match them by name and note.
- Overview and Entries totals: "Expenses" shows the net figure and, when
  `Returned > 0`, a caption "after {Returned} returned".
- Strings go to `BudgetsStrings` (English and Polish).

## 5. Docs

- `specs/money-design.md`: §3.2 `ReturnOf`, §3.3 a pointer to this spec,
  §3.4 the new events.
- `docs/src/pages/features.astro`: returns under Budgets.

## 6. Testing

- Unit (`PSPad.Module.Money.Tests`): every rule in §2.3 and its rejection
  text; the cascades on edit and delete; category rename and merge carrying
  returns; `ReturnState`; `SignedPln` in `MonthTotals`, `MoneySummary`,
  `UsualMonth`; `RateSuggestion` ignoring returns.
- Integration (`PSPad.Api.Tests`): `RecordReturn`, `EditReturn` and
  `DeleteEntry` cascade through the real pipeline against MongoDB; the
  over-return rejection.
- App (`PSPad.App.Tests`, bUnit): `EntryRow` return and chip states;
  `EntryPanel` Returns section; `ReturnPanel` prefill, cap and save;
  `EntryQuery` routes; `EntryFilters` with returns.
- E2E: record a purchase, a partial return, check the Expenses total and
  the chip; delete the purchase and see the return gone.
