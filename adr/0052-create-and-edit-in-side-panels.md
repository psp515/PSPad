---
title: Create and edit every thing in its side panel, never a popup
tags: [ui, consistency]
date: 2026-09-30
status: Active
---

# ADR-0052: Create and edit every thing in its side panel, never a popup

## Context

Tasks, reference items, areas, goals and Inbox items were created and
edited in the query-addressed `DetailPanel`, but lists were renamed through
`NameDialog` and moved through `AreaPickerDialog`. Adding a reference item
opened the new item, while adding a task, list or goal closed the panel.
Area, list and goal panels used an outlined **Name** box, task and
reference-item panels an unboxed heading field. The maintainer asked for
every element to behave uniformly.

## Decision

We will create and edit every thing in its side panel. Each panel opens
with the unboxed heading name field; every other property sits below it.
Editing saves as it changes; adding closes the panel and never opens the
new thing, except a new area, which lands on its own board. Card menus
offer **Edit** and **Delete** only. Popups remain only for delete
confirmations and for pickers opened from inside a panel (due date, custom
repeat, custom accent). Lists gain an edit mode in `ListDetailPanel`
(`?list={id}`) holding name, read-only kind, area and delete;
`NameDialog` and `AreaPickerDialog` are removed.

## Considered alternatives

- **Keep small popups for one-field edits (rename, move)** — fastest for a
  single change, but it is exactly the inconsistency being removed, and it
  splits a list's properties across two dialogs and a panel.
- **Open the new item after adding, everywhere** — handy for adding labels
  straight away, but it breaks rapid entry of several things in a row,
  which is the common case for tasks and lists.
- **Close after adding a new area too** — uniform, but a new area is empty;
  its board is where the next action (a first list) happens.

## Consequences

One interaction model: a user who learned one panel knows all of them, and
every edit is linkable and closed by Back. Renaming a list takes a panel
open instead of a small dialog. Adding a reference item with many labels
now closes afterwards; the labels are still added from the draft before it
closes. Delete confirmations stay modal, so destructive actions still need
an explicit second step.
