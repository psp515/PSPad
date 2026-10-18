---
title: Store the user's language on the User aggregate and localize with .NET resources
tags: [identity, ui, offline, localization]
date: 2026-10-18
status: Proposed
---

# ADR-0075: Store the user's language on the User aggregate and localize with .NET resources

## Context

The app is English only. Every string sits in a `.razor` file, and date
formatting mixes `InvariantCulture` with the browser's culture. The
maintainer wants English and Polish now, with a path for contributors to add
a language without touching components. The app is offline-first: the first
paint must be in the right language before any server round trip, and a
language change made offline must stick. Keycloak fronts sign-in and could
hold a locale, but the app already treats account-level settings (time zone)
as its own data on the `User` aggregate.

## Decision

We will store the language as `User.Language`, validated against a single
`SupportedLanguages` list in `PSPad.Abstractions`, changed through
`PUT api/me/language` like the time zone, and cached per device in
`localStorage` so the WASM host sets `CultureInfo` before the first render.
We will localize strings with `Microsoft.Extensions.Localization` and `.resx`
files grouped per feature, one whole sentence per entry, and let the culture
drive date and number formatting. Domain rejection messages stay English and
modules carry no resources. Keycloak stores nothing and its realm is not
changed. Public snapshot pages follow the viewer's browser language.

## Considered alternatives

- **Browser-only preference (`localStorage` like the theme)** — no server
  change, but every device must be set separately and the server can never
  localize anything later (reminders, emails).
- **Keycloak `locale` attribute read from the token** — Keycloak owns the
  setting; changing it needs the Keycloak account API, which does not work
  offline and bypasses the app's own settings surface.
- **Error codes on `DomainRejectedException` with client-side lookup** —
  fully localized rejections, but touches every `throw` in every module for
  messages that appear rarely in a snackbar. Deferred; the exception type can
  gain a code later without changing this decision.
- **JSON-based localization packages (`Toolbelt.Blazor.I18nText`,
  `Blazored`)** — another dependency, runtime-fetched files that the service
  worker must also cache, no tooling support in IDEs.
- **Hosted translation platforms (Tolgee, Crowdin SDK)** — over-the-air
  strings contradict self-hosting and offline use.
- **Enabling Keycloak internationalization and passing `kc_locale`** —
  cheap, but the maintainer prefers zero Keycloak coupling; the login page
  stays in Keycloak's default language.

## Consequences

Easier: one language setting that follows the user across devices; first
paint localized offline; standard .NET tooling; adding a language is a
resource file plus one list entry; dates and numbers become consistent with
the language.

Harder: a language change reloads the WASM host (culture cannot switch
live); Polish pulls the larger `icudt_no_CJK.dat` ICU shard (about 1.4 MB,
cached once); plural handling needs a small helper because `.resx` has no
plural rules; rejection messages and the Keycloak login page remain English,
which users will notice.

If wrong: the `User.Language` field and the `.resx` files survive any later
switch of lookup mechanism; only `IStringLocalizer` call sites would change.
