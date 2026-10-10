---
title: Money is its own WASM-safe module, with budgets as containers and rates frozen on every amount
tags: [architecture, modularity, domain, offline, sync, integrations]
date: 2026-10-09
status: Active
---

# ADR-0059: Money is its own WASM-safe module, with budgets as containers and rates frozen on every amount

## Context

Issue #109 asks PSPad to track expenses, incomes and monthly balances, in
several currencies, and to keep working offline. Every total is in PLN, and
the PLN value of an amount has to stay as it was on the day it was entered,
so the user can later compare it with today's rate. The issue says money is a
separate module. Sharing a budget with someone else, such as a household
partner, isn't needed yet, but the maintainer wants the design not to get in
its way. The maintainer also wants rates typed by hand, with NBP (the Polish
central bank) only filling in a suggestion when the user asks for one.

## Decision

We will add `PSPad.Module.Money`. Like Tasks, it references
`PSPad.Abstractions` only, so its commands and handlers run in the browser
for offline use and on the server (AD-3, AD-4). Only `PSPad.Api` and
`PSPad.App` reference it.

It holds four aggregates, each one document per aggregate (AD-2):

- **`Budget`**: the container. It holds a name, the owner, the expense and
  income category lists, and `ArchivedAt?`. A budget is archived, never
  deleted. While archived, it and everything in it reject writes.
- **`MoneyEntry`**: one expense or income. It carries `BudgetId`.
- **`BalanceSnapshot`**: one per budget per month, with id
  `IdFor(budgetId, yyyy-MM)`. It holds named holdings of type cash, savings
  or investment.
- **`MoneyPreferences`**: one per user, holding the default currency.

Every amount is a `Money(Amount, Currency, RateToPln, RateDate)` value. The
rate is frozen on the entry or holding when it is saved. There is no rate
table:
- The panel pre-fills the rate from the last rate used for that currency in
  the budget.
- `GET /api/money/nbp-rate` is an online-only, server-side endpoint in
  `PSPad.Api`, outside the module and outside the command pipeline. It
  returns NBP table A's mid rate for one currency on one date. The user
  accepts or overrides it.

Every Money document's `UserId` is the budget's owner, and every command goes
through one function, `BudgetAccess.To(budget, actorId)`. v1 grants the owner
only, so sharing can be added later by widening that function and the sync
filter, following `adr/0054`.

Recording an entry under a category the budget doesn't have adds it within
the same command. Renaming or merging a category relabels its entries in one
unit of work, one event per entry, using the `adr/0042` cascade pattern.

Summaries and charts are pure functions in the module, computed on the
client from the local replica. Statistics projects nothing from Money.

## Considered alternatives

- **One ledger document per month.** Fewer documents, but under last write
  wins (AD-5), two devices editing the same month overwrite each other's
  entries.
- **A per-user money book with no budget container.** Simpler today, but
  sharing would later need a migration that adds a container id to every
  entry and re-keys every snapshot. Snapshots are frozen history, which is
  exactly what should never be rewritten.
- **A rate table per budget, filled from NBP by a daily server job or on
  demand.** It duplicates what each entry already stores, adds a document
  every device can conflict on, and in the daily-job variant adds a
  background dependency. The maintainer chose per-entry rates with an
  on-demand NBP suggestion.
- **The browser calling NBP directly.** Every device fetches separately, it
  depends on NBP's CORS policy, and there's no single place to cache or
  test it.
- **Server-side Statistics-style projections for summaries.** The charts
  would need the network, which breaks offline viewing, and a few thousand
  entries a year don't need pre-aggregation.
- **The default currency on Identity's `User`.** That would make Identity
  aware of money. A per-user Money document keeps the boundary clean.

## Consequences

- **Offline:** recording, editing and viewing money all work offline, with
  the same handlers on both sides.
- **Stable totals:** totals never shift when rates move, because each amount
  carries its own frozen rate.
- **Sharing later** is additive: members on `Budget`, a wider
  `BudgetAccess`, a wider sync filter.
- **New sync collections:** four new collections join sync (`adr/0049`
  covers existing replicas) and the generic account wipe.
- **Self-hosting:** the API now needs outbound HTTPS to `api.nbp.pl`, for the
  NBP button only. Without it the button fails and nothing else does.
- **Category drift:** a category renamed on one device while another, offline
  device still records under the old name comes back as a separate category.
  The user merges them. There is no cross-aggregate reconciliation.
- **Client-side summaries** load every entry of a budget into memory. If
  budgets grow to tens of thousands of entries, this decision has to be
  revisited, probably with a projection.
- **No delete:** with archive only, a budget created by mistake stays
  (archived) forever.
