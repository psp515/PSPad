---
title: Organize the backend as vertical-slice folders in one project
tags: [architecture, modularity]
date: 2026-09-11
status: Superseded by ADR-0010
---

# ADR-0001: Organize the backend as vertical-slice folders in one project

## Context

Slice 1 needed a starting shape for the codebase before any code existed.
The GTD core has clearly separable features (Areas, Inbox, Tasks, Goals,
Today, History) but a single person was going to build all of them, and a
premature split into services would slow that down for no reader benefit
yet.

## Decision

We will structure the backend as a modular monolith: one project, with each
feature as a folder (Areas, Inbox, Tasks, Goals, Today, History) holding its
own commands, handlers, projections and endpoints. No horizontal
Services/Repositories layering, no microservices.

## Considered alternatives

- **Microservices per feature** — rejected: no independent scaling or
  deployment need exists yet, and the operational cost (service discovery,
  network calls between Areas and Tasks) buys nothing at this scale.
- **Traditional layered architecture** (Controllers/Services/Repositories
  cutting across all features) — rejected: forces every feature change to
  touch three unrelated folders instead of one.

## Consequences

Fast to start, easy to navigate by feature. But everything lives in one
project, so nothing stops a feature from reaching into another's internals,
and nothing enforces that the domain stays free of infrastructure — both
gaps this ADR left open. When the store changed from PostgreSQL/Marten to
MongoDB and the client needed to run the same domain code in WebAssembly,
those enforcement gaps mattered enough to redraw the boundary as separate
projects. See ADR-0010.
