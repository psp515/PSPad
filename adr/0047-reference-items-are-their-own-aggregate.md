---
title: Reference lists hold ReferenceItems, a separate aggregate, not a kind of task
tags: [domain, aggregates, ui]
date: 2026-09-28
status: Proposed
---

# ADR-0047: Reference lists hold ReferenceItems, a separate aggregate, not a kind of task

## Context

Users want lists of things that are never done: filaments (colour, amount
left, temperature), links to documents on OneDrive, folders on disk. Each
needs a name, a description and a set of labelled values instead of steps.
AGENTS.md kept "list types beyond plain" out of slice 1 and names reference
materials and the print domain as later subsystems. Design:
`specs/reference-lists-design.md`.

## Decision

We will give `TaskList` an immutable `Kind` (`Tasks` or `Reference`) and model
a reference entry as a new `ReferenceItem` aggregate in `PSPad.Module.Tasks`,
stored in `referenceitems`, carrying `Name`, Markdown `Description`, `Starred`,
`Position` and ordered `ReferenceField(Label, Value, Display?)`. Field display
kinds (text, link, path, quantity) are a client rendering hint stored as an
opaque string; the domain never interprets them. A task lives only in a
`Tasks` list, a reference item only in a `Reference` list, and an Inbox item
only ever becomes a task.

## Considered alternatives

- **A `Kind` on `TodoTask`** — one aggregate with dozens of "not for a
  reference" rejections (due date, steps, completion, recurrence, goal); the
  Today rule and Statistics would need filters, and `TaskCreated` from a
  reference would inflate "opened" counts. Every future task feature would
  have to remember the exception.
- **A kind per item inside ordinary lists** — reference collections are
  whole lists of like things; mixing them into task lists makes every list
  view and the Today rule branch per row.
- **A separate `PSPad.Module.Reference`** — items must check the kind of a
  list, which lives in Tasks, so the module would depend on Tasks for no
  isolation gain.
- **A per-list field schema** — a typed column system and editor; worth it
  only with the print domain, and free-form labels do not block adding one.

## Consequences

Today, Statistics and goals are untouched. Sync and the replica gain one
collection. Deleting a list or area cascades to its reference items. A later
print domain can extend `ReferenceItem` rather than `TodoTask`. The price is
a second item aggregate with its own commands and UI, and some duplication
with steps (field ordering mirrors step ordering).
