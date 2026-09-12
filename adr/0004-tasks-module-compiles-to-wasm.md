---
title: The Tasks module must compile to WebAssembly with zero infrastructure references
tags: [architecture, offline, purity]
date: 2026-09-11
status: Active
---

# ADR-0004: The Tasks module must compile to WebAssembly with zero infrastructure references

## Context

ADR-0003 commits to running the same command-handling code on the server
and in the browser. That only works if the code in question has no
dependency the browser sandbox cannot satisfy — no database driver, no
`System.Net.Http`, no ASP.NET hosting types.

## Decision

`PSPad.Module.Tasks` (areas, lists, Inbox, tasks, steps, goals, recurrence,
Today) references `PSPad.Abstractions` and nothing else. A purity guard test
fails the build if an infrastructure reference sneaks in, and a second guard
keeps `PSPad.Infrastructure` from ever referencing a module.

## Considered alternatives

- **Trust code review to catch stray references** — rejected: a
  `using Microsoft.Extensions.Hosting;` that compiles fine on the server is
  invisible until someone tries to publish the WASM client, by which point
  the offending code may be deep in a merged PR.
- **Compile-time conditional code (`#if BROWSER`)** — rejected: this is
  exactly the "two models of behavior" ADR-0003 exists to avoid; a rule that
  only runs on the server is a rule the offline client can silently violate.

## Consequences

A stray dependency fails fast, at build time, in a test — not at publish
time, and not in production. The cost is discipline: anything the domain
needs (clocks, ids, ordering) has to be expressed as an abstraction in
`PSPad.Abstractions` rather than reached for directly, which is slower to
write than pulling in a convenient library.
