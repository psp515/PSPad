---
title: Shares received carry their cost basis as Money, move no cash, and count it as passive income
tags: [domain, money, investments, import]
date: 2026-11-03
status: Active
---

# ADR-0089: Shares received carry their cost basis as Money, move no cash, and count it as passive income

> Status: Active (narrows AD-14, `adr/0059`, and `adr/0087` for one
> operation kind)

## Context

A corporate action such as a spin-off gives shares without a purchase. The
XTB export has no `Stock purchase` row for them, only the later `Stock
sell`, so the import saw a sell with no lot. Every investment operation's
`Money` was its cash effect, always more than 0 (AD-14), and the profit
identity (total profit = realized + unrealized + passive income − fees)
rested on every non-cash change of value showing up in one of those terms.

## Decision

We will add the operation kind `SharesReceived`. It needs a ticker, a
quantity and a price, opens a lot like a buy, and moves no cash
(`SignedAmount` is 0). Its `Money` is the cost basis of the received shares
(quantity × price, rounded to 2 decimals) in the account's currency, with
the frozen NBP rate of its day as on every amount; it is the one kind whose
amount may be 0. The cost basis counts as passive income, in the account
currency and in PLN (at the FX cost pool's rate, `adr/0087`), so the profit
identity stays exact. An imported one takes a deterministic id from the XTB
position id with its own salt.

## Considered alternatives

- **A Buy with a zero amount and a matching Deposit.** Would keep "Money is
  cash", but shows money put in that never arrived and distorts net
  deposits.
- **No cost (amount 0 always).** Simple, but the broker reports a cost
  basis and its realized result would no longer match the broker's.
- **Count the cost basis inside net deposits.** It is not money the user
  put in; deposits must match the bank.
- **Split the parent's cost basis.** Correct for tax, but needs an
  allocation ratio XTB does not export; left out.

## Consequences

- Sells of received shares realize proceeds − cost basis, like the
  broker's own figure, and the identity holds per account and in PLN.
- `Money` no longer means "cash moved" for this one kind; every reader of
  `SignedAmount` already gets 0, but code summing `Money.Amount` by kind
  must leave SharesReceived out of cash sums.
- The parent keeps its full cost; a hand-entered spin-off at a realistic
  price shows that value as passive income.
- An older client that syncs a SharesReceived document sees an unknown enum
  value: it reads it as cash out and misses its lot (sells show as
  oversold), and rejects edits to it, until it updates.
