---
title: Quotes come from Yahoo Finance through a cached server proxy
tags: [architecture, http, storage, deployment, investments]
date: 2026-10-30
status: Active
---

# ADR-0084: Quotes come from Yahoo Finance through a cached server proxy

> Status: Active

## Context

Positions, charts and the budget link need daily closing prices for Warsaw,
US, UK and German shares and ETFs. Free sources were weighed: Stooq now sits
behind a JavaScript proof-of-work page, other APIs need keys or cover the
US only. Yahoo Finance's chart endpoint covers all of these without a key,
but it is unofficial, rate-limited and can change. Prices are public data,
the same for every user of an instance, and the app must keep working
offline.

## Decision

We will fetch daily closes from
`query1.finance.yahoo.com/v8/finance/chart/{symbol}` on the server only,
behind `GET /api/investments/quotes`, built like the NBP client: a 10 s
timeout, `404` for an unknown symbol, `502` for a failure. Each symbol's
series is cached in a Mongo collection `quotes` shared by the instance and
refetched at most once a day, or after 15 minutes when the client asks for a
refresh. On failure a cached copy is returned marked stale. The client keeps
the last series per symbol in IndexedDB for offline use. Rates for
conversion come from a new NBP series endpoint beside the single-day one.

## Considered alternatives

- **The browser calling Yahoo.** CORS blocks it, every device would fetch
  separately, and there is no single place to cache or test.
- **Stooq.** Good Warsaw coverage, but its bot challenge would have to be
  bypassed, which we will not do.
- **A keyed provider (Alpha Vantage, Twelve Data, EODHD).** Each self-hoster
  would need an account and an env var, and free tiers are small.
- **A daily server job refreshing every held symbol.** A background
  dependency for data that is only needed when someone looks.

## Consequences

- One fetch per symbol per day per instance, however many users hold it.
- The API container needs outbound HTTPS to `query1.finance.yahoo.com`;
  without it positions show cost values and "No price".
- If Yahoo changes or blocks the endpoint, prices stop until the client in
  `PSPad.Api/Quotes` is fixed or replaced; nothing else depends on the
  source.
- `quotes` holds public data and is neither synced nor wiped with accounts.
