---
title: Investment PLN totals use the PLN actually spent on a currency; the frozen NBP rate stays as the fallback
tags: [domain, money, investments]
date: 2026-11-01
status: Active
---

# ADR-0087: Investment PLN totals use the PLN actually spent on a currency; the frozen NBP rate stays as the fallback

> Status: Active (narrows how AD-14's frozen rate, `adr/0059`, is used in
> investment PLN totals)

## Context

Every amount carries the NBP rate of its day, frozen at entry (AD-14,
`adr/0059`), and the investments PLN totals converted each operation at that
rate (`specs/investments-design.md` §5.3). Real XTB exports show that a USD
or EUR account is funded by currency conversions from the PLN account, at
the broker's rate, not NBP's. The user paid that rate: converting the USD
account's deposits and purchases at NBP's rate of each day misstates what
the money cost in PLN, and the "currency difference" line then mixes the
broker's spread with real exchange-rate moves. Each conversion row now
carries the other leg's amount (`CounterMoney`), so the PLN actually spent
is known.

## Decision

We will value a non-PLN investment account's operations in PLN at the
average rate actually paid for its currency. Per account, a running pool of
(currency units, PLN cost) is kept in operation order: a CurrencyConversion
TransferIn whose counter amount is in PLN adds (amount, counter PLN); a
CurrencyConversion TransferOut to PLN removes units at the pool's average
rate. Each operation's PLN value is its amount × the pool's average rate at
its time. While the pool is empty, the operation's frozen NBP rate is used.
The frozen NBP rate is still stored on every amount, unchanged; values
today still use today's NBP rate, so the currency difference is what NBP
today adds over the average rate paid.

## Considered alternatives

- **Keep the frozen NBP rate everywhere.** Simple and already built, but
  the PLN figures would not match the PLN the user spent, which is the
  number the user checks against the bank and the broker.
- **Use the conversion rate only on the conversion rows.** Purchases and
  dividends between conversions would still use NBP rates, so the same
  dollars would carry two different PLN costs.
- **FIFO per conversion lot.** Exact per unit, but needs a second lot book
  for cash, and the average is what the broker and the user reason with.

## Consequences

- PLN totals of a USD or EUR account reflect the broker's rate and spread;
  the account can show "Average rate 1 USD = 3.6512 PLN" and "PLN spent on
  USD".
- The frozen NBP rate stays the truth for everything else (budgets,
  holdings) and the fallback for an account with no conversions, such as one
  funded by a foreign deposit or entered by hand.
- PLN figures of past operations change when an earlier conversion is
  imported or edited later. They are derived on read, so nothing stored
  changes.
- A conversion between two non-PLN currencies does not feed the pool; its
  operations fall back to the frozen rate until a PLN conversion exists.
