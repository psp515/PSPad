---
title: A return is an expense entry linked to its purchase, at the purchase's frozen rate
tags: [domain, money, storage, sync]
date: 2026-11-10
status: Active
---

# ADR-0090: A return is an expense entry linked to its purchase, at the purchase's frozen rate

> Status: Active (narrows `adr/0059` for one entry shape)

## Context

People return things they bought. Editing or deleting the expense loses the
fact that the money was spent, and recording the refund as income inflates
income and leaves the category's spending wrong. A return happens on its own
day, often in a later month, and may cover part of a purchase. Every
`MoneyEntry` is an aggregate of its own, last-write-wins offline (AD-5), and
every amount carries a rate frozen at entry (AD-14).

## Decision

We will record a return as a `MoneyEntry` of kind `Expense` with `ReturnOf`
set to its purchase's id. It takes the purchase's name, category, currency
and frozen rate; only its amount, date and note are its own. Readers count
it as a negative expense on its own date (`SignedPln`). The total returned
never exceeds the purchase: `RecordReturn` and `EditReturn` load the budget
owner's entries and check the sum, the way category cascades already do.
Editing a purchase's name or category relabels its returns, and deleting a
purchase deletes its returns, in the same unit of work. A purchase with
returns keeps its currency and rate.

## Considered alternatives

- **A returned amount on the expense itself.** One document, and the cap is
  a local rule, but the return can't fall in its own month and a purchase's
  history of returns has nowhere to live.
- **A `Return` value in `CategoryKind`.** That enum also names the budget's
  category lists, so every category rule would need a third case.
- **A separate `MoneyReturn` aggregate.** A new collection, store, sync
  type and replica table for a shape that is an entry in all but its link.
- **The rate on the return's date.** True to the exchange rate, but a full
  return would leave a currency difference in an expense category.

## Consequences

- Category rename, merge and remove cover returns without new code; sync,
  storage, sharing and the replica need no change beyond one nullable field.
- The cap spans documents, so two offline devices can each pass it; the
  later one is rejected on the server and surfaced (AD-5).
- A category's monthly total can be negative when a return lands after its
  purchase's month. Readers and charts must tolerate it.
- Editing and deleting an entry now load all of the owner's entries, as
  category commands already do.
