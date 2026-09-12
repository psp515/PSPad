---
title: Integration tests own a real, disposable MongoDB via Testcontainers
tags: [testing, persistence]
date: 2026-09-12
status: Active
---

# ADR-0009: Integration tests own a real, disposable MongoDB via Testcontainers

## Context

The MongoDB replanning (2026-09-12) made multi-document transactions a hard
dependency (they require a replica set — see ADR-0011), and transaction and
BSON serialization behavior is exactly the kind of thing an in-memory fake
cannot faithfully reproduce. The dev-time compose stack runs MongoDB too,
but sharing it with the test suite would mean tests could observe each
other's data and could not run against a clean database on demand.

## Decision

Integration tests start their own MongoDB 8 replica set per test run via
Testcontainers — one container shared by an xUnit collection, not one per
test, and never the compose service used for local development. Tests
isolate from each other by using a distinct `UserId` per test rather than
dropping collections between tests.

## Considered alternatives

- **Share the dev compose MongoDB with the test suite** — rejected: couples
  test runs to whatever state a developer's local database happens to be
  in, and makes CI runs interfere with a developer's manual testing on the
  same machine.
- **An in-memory MongoDB fake** — rejected: the thing under test is
  transaction and serialization behavior; a fake that doesn't implement
  real transactions would pass tests that a real replica set would fail.
- **One container per test** — rejected: MongoDB replica-set startup takes
  seconds; per-test startup makes the integration suite unusably slow.

## Consequences

Integration tests exercise the real thing — a replica set, real
transactions, real BSON — so a passing suite means the persistence layer
actually works, not that a fake agreed with itself. The cost is one
container's startup time per test run (paid once, shared by the collection)
and a hard dependency on Docker being available wherever these tests run.
