---
title: Budget CSV import runs on the client as ordinary commands
tags: [architecture, contracts, offline, money]
date: 2026-10-13
status: Proposed
---

# ADR-0074: Budget CSV import runs on the client as ordinary commands

> Status: Proposed

## Context

Users want to bring budget history from spreadsheets: expenses, incomes and
monthly balances, often thousands of rows. Money's commands are the shared
contract and run the same handler in WASM and on the server (ADR-0003,
ADR-0059). The server caps a command batch at 50 and limits each user's
command requests with a token bucket (ADR-0062), and Kestrel caps a request
body at 1 MB. A user must see what an import will do (errors, duplicates,
new categories, rates fetched from NBP) before anything is written. The
design is in `specs/budget-import-design.md`.

## Decision

We will parse and preview CSV imports in the browser, in pure code in
`PSPad.Module.Money/Import`, and turn the confirmed rows into the existing
commands (`RecordExpense`, `RecordIncome`, `StartBalanceMonth`,
`AddHolding`, `EditHolding`, `RemoveHolding`). They run through the client's
local dispatcher and the outbox like hand-entered commands. The import adds
no command, event, endpoint or domain rule. A month the import creates holds
exactly the file's rows: after `StartBalanceMonth` carries the previous
month forward, the import edits or removes the copied holdings.

## Considered alternatives

- **Server upload endpoint** that parses the file and runs the handlers.
  It is online only, the 1 MB body cap limits files, and it is a second
  write path beside the shared command contract.
- **One bulk `ImportMoneyEntries` command** carrying every row. One bad row
  rejects the whole batch, payloads hit the 1 MB cap, and it needs a new
  multi-aggregate command in both hosts.
- **A `CarryForward` flag on `StartBalanceMonth`** so an imported month
  starts empty. It works, but it changes a shared command for one caller,
  whereas editing and removing the copied holdings reaches the same state
  with existing commands.

## Consequences

- Imports work offline except for NBP lookups, rows are accepted or
  rejected one at a time, and the preview reuses domain validation, so the
  rules cannot drift.
- A large import is thousands of commands and events, which the outbox ships
  over minutes under the rate limit. `CommandSender` needs a `SendManyAsync`
  so it does not sync and re-render after every command.
- An imported new month briefly holds copied holdings before the edits and
  removes, and those steps stay in the event log.
- There is no undo as a unit: the import's entries are ordinary entries.
