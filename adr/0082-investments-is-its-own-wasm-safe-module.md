---
title: Investments is its own WASM-safe module
tags: [architecture, modules, offline, money, investments]
date: 2026-10-30
status: Active
---

# ADR-0082: Investments is its own WASM-safe module

> Status: Active

## Context

Issue #115 asks for brokerage accounts, their operations and positions, profit
per account and in total, price charts, and a way to put an account's value
into a budget's balance on a chosen day. Money already has an Investment
holding type, but it is one typed number with no units, cost or history
(`specs/money-design.md` §1 lists investment detail as out of scope). The
data is personal, works offline like the rest of PSPad, and is imported from
XTB's xlsx export. The design is in `specs/investments-design.md`.

## Decision

We will add `PSPad.Module.Investments`, referencing `PSPad.Abstractions`
only, with four aggregates: `InvestmentPortfolio` (one per user, listing its
accounts and tickers so per-user caps are checked inside one aggregate),
`InvestmentAccount` (which counts its operations for the per-account cap),
`InvestmentOperation` (one document per operation) and `Instrument` (a
per-user ticker → quote symbol mapping). Its commands run the same handlers in WASM and on the server, its
collections join delta sync, and its readings (lots, figures, value over
time) are pure code shared by both hosts. Accounts are private to their
owner. Money never references Investments: the budget link is a `Guid` on
the holding (`adr/0085`), and the value is computed in `PSPad.App`.

## Considered alternatives

- **Inside `PSPad.Module.Money`.** Fewer projects and no shared-value move,
  but Money would grow a second domain with its own aggregates, import and
  quotes, and budgets would carry knowledge of lots and prices.
- **One document per account holding its operations.** Fewer documents, but
  under last write wins (AD-5) two devices adding operations offline
  overwrite each other, and a 20,000-operation account outgrows a document.
- **Server-only module with server-computed figures.** Charts and positions
  would need the network, which breaks offline viewing, the same reason
  Money rejected projections (`adr/0059`).

## Consequences

- Investments work offline, except for fetching new prices and rates.
- Every figure is recomputed on the client from operations; a few thousand
  operations are cheap, but a very large history will cost CPU on phones.
- Every resource has a count cap (32 accounts, 500 instruments, 20,000
  operations per account). Recording an operation writes two documents, the
  operation and its account's count, and creating an account or instrument
  writes the portfolio too.
- Cross-document rules other than counts (selling more than held) cannot be
  enforced in `Decide`; the reading layer flags them instead.
- A third WASM-safe module needs `Money` and `Currencies` in Abstractions
  (`adr/0083`).
