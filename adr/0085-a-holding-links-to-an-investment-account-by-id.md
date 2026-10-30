---
title: A holding links to an investment account by id, and is a copy refreshed on demand
tags: [architecture, contracts, money, investments]
date: 2026-10-30
status: Active
---

# ADR-0085: A holding links to an investment account by id, and is a copy refreshed on demand

> Status: Active

## Context

A budget's balance should show investment accounts at their value on a day
the user picks, one holding per account. Balances are frozen history:
a past month must not change when prices move. Budgets can be shared, while
investment accounts are private. Money must not reference Investments
(`adr/0082`).

## Decision

We will add an optional `InvestmentAccountId` (`Guid`) to `Holding`, carried
by `AddHolding`, `EditHolding`, their events and `StartBalanceMonth`. Money
stores it and allows one holding per linked account in a month, and checks
nothing else about it. `PSPad.App` computes the account's value on the chosen
day, refreshing prices first, and sends ordinary holding commands with the
amount and a frozen NBP rate. "Refresh from investments" repeats that on
demand. Only the account's owner can add or refresh it.

## Considered alternatives

- **A live link** that values the holding whenever the budget is read.
  Past months would change with prices, members of a shared budget would
  need access to private accounts, and Money would depend on Investments.
- **No link, just a copy.** Simple, but a refresh could not find which
  holding belongs to which account, and carrying forward would lose it.
- **A link table in Investments.** A second document to keep consistent
  with snapshots under last write wins.

## Consequences

- Balances stay frozen; a refresh is an explicit edit in the event log.
- Members of a shared budget see the value but cannot refresh it.
- A deleted or archived account leaves its holdings as plain numbers.
- `Holding` and two commands gain a field; old documents and commands read
  it as absent.
