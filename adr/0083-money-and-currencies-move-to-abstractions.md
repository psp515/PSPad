---
title: Money and Currencies move to Abstractions, keeping their namespaces
tags: [architecture, modules, money, investments]
date: 2026-10-30
status: Active
---

# ADR-0083: `Money` and `Currencies` move to Abstractions, keeping their namespaces

> Status: Active (amends [0059](0059-money-is-its-own-wasm-safe-module.md))

## Context

Investment operations carry an amount with a frozen rate to PLN (AD-14), and
accounts take a currency from the same list budgets use. `Money` and
`Currencies` live in `PSPad.Module.Money`, and a WASM-safe module may
reference `PSPad.Abstractions` only. The record is named `Money`, which is
why it sits in namespace `PSPad.Module.Money`: inside any
`PSPad.Module.Money.*` namespace, the simple name `Money` resolves to the
namespace before any `using`, so a type of that name in another namespace
would not be found there.

## Decision

We will move `Money` and `Currencies` into the `PSPad.Abstractions` assembly
and keep their namespaces, `PSPad.Module.Money` and
`PSPad.Module.Money.Values`. Nothing that uses them changes: no call site,
command, event, contract or stored document. Investments imports those
namespaces like any other.

## Considered alternatives

- **Rename the type** (for example to `Amount`) in a neutral namespace.
  Cleaner names, but it touches every Money command, event, panel and test,
  and the JSON contracts, for no behaviour change.
- **Copy the two types into Investments.** Two currency lists and two sets
  of amount rules drift apart.
- **Let Investments reference Money.** It breaks the rule that WASM-safe
  modules see Abstractions only and couples two domains.

## Consequences

- One currency list and one amount rule for both modules.
- A namespace that names a module lives in another assembly, which is
  surprising when reading the project tree; a short note in
  `specs/modules-spec.md` points to this record.
