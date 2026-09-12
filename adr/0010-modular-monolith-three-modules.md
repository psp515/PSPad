---
title: Split the modular monolith into three separate module projects
tags: [architecture, modularity]
date: 2026-09-12
status: Active
---

# ADR-0010: Split the modular monolith into three separate module projects

## Context

ADR-0001's vertical-slice folders lived in one project, which meant nothing
stopped a Tasks feature from reaching into History internals, and nothing
enforced that the domain stayed free of infrastructure. The MongoDB
replanning made that second gap urgent: the same domain code now had to
compile to WebAssembly (ADR-0004) and run unmodified in the browser, which
a folder convention cannot guarantee — only the compiler can.

## Decision

Supersedes ADR-0001. The backend is a modular monolith split into three
module *projects*, not folders: `PSPad.Module.Tasks` (areas, Inbox, lists,
tasks, steps, goals, recurrence, Today), `PSPad.Module.History` (queries
over the event log), `PSPad.Module.Identity` (User, time zone, first-sign-in
provisioning). Inside a module, features are folders holding their own
commands, events, aggregate and handlers. References run one way only:
`PSPad.Api` sees everything and is the only place a module meets MongoDB;
`PSPad.Infrastructure` never sees a module; `PSPad.Module.Tasks` sees only
`PSPad.Abstractions`.

## Considered alternatives

- **Keep folders, add an architecture test for cross-folder references
  only** — rejected: a project reference is something the compiler can
  refuse to resolve; a folder boundary can only be checked after the fact
  by a test crawling namespaces, which is weaker and slower to fail.
- **Microservices** — rejected for the same reason as in ADR-0001: no
  operational need justifies the cost at this scale.

## Consequences

A stray reference from `PSPad.Module.Tasks` to MongoDB or ASP.NET now fails
at compile time, not at a test run or a WASM publish. The cost is more
projects to keep track of, and every new cross-module capability has to be
threaded through `PSPad.Abstractions` or `PSPad.Contracts` rather than
reached for directly.
