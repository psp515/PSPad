---
title: Each integration is its own module, driving Tasks through server-only commands
tags: [architecture, modularity, integrations, domain]
date: 2026-09-28
status: Proposed — pattern reused by `PSPad.Module.Sharing` (adr/0056), the
  first module outside an integration to drive Tasks through
  `IServerOnlyCommand`s
---

# ADR-0045: Each integration is its own module, driving Tasks through server-only commands

## Context

GitHub is the first external sync (AGENTS.md §9). Users want a repository's
open issues and pull requests as a list, planned locally (due date, star,
steps) so they reach Today, while GitHub stays the owner of title and state.
OneDrive, Google Drive and Thingiverse follow the same shape later. Design:
`specs/github-integration-design.md`.

## Decision

We will model a mirrored item as an ordinary `TodoTask` carrying a generic
`ExternalRef`, inside a `TaskList` carrying an `ExternalSource`, inside an
`Area` flagged `Managed`. Tasks' `Decide` rejects user commands on fields the
source owns. The source writes through server-only commands — marked
`IServerOnlyCommand` in `PSPad.Abstractions` and excluded from
`CommandCatalogue` — that live in Tasks and run through the normal handler
and unit of work. Each integration gets its own server-only module
(`PSPad.Module.GitHub` first) referencing Tasks one way, never the reverse.

## Considered alternatives

- **A separate `ExternalItem` aggregate in the integration module** —
  duplicates the Today rule, Statistics projections, steps and planning UI,
  or forks them for a second type; breaks "one Today rule, one place".
- **A read-only projection plus a planning overlay aggregate** — two new
  shapes, and Today must still join two sources.
- **One shared `PSPad.Module.Integrations`** — mixes unrelated providers'
  HTTP clients, auth and mapping in one assembly; per-provider modules keep
  each removable.

## Consequences

Today, Statistics, offline replica and delta sync work for mirrored tasks
unchanged, and `TaskCompleted` from a merged PR counts like any completion.
Tasks gains three source-agnostic concepts and a set of rejections to test.
A second module-to-module edge exists (GitHub → Tasks), guarded one way like
Statistics → Tasks. Server-only commands are a new category the catalogue
must filter; a guard test keeps every mirror command marked.
