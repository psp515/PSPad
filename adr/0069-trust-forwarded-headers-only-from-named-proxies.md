---
title: Trust forwarded client addresses only from named proxies
tags: [http, security, deployment]
date: 2026-10-13
status: Active
---

# ADR-0069: Trust forwarded client addresses only from named proxies

## Context

The anonymous `public` rate limit and the `join` limit's anonymous fallback
partition requests by `Connection.RemoteIpAddress`. Production runs the API
behind a Cloudflare Tunnel (`cloudflared`) with forwarded headers off, so
every request arrived from the connector's container address and all
visitors shared one bucket of 60 requests a minute: one client hammering a
snapshot link blocked every public snapshot for everyone, and logs held no
real client address. The shipped switch, `API_BEHIND_PROXY` mapped to
ASP.NET Core's `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, clears
`KnownProxies` and `KnownNetworks`, so with port 5000 reachable anyone
could forge `X-Forwarded-For` and mint a fresh bucket per request
(OWASP A04/A05, issue #142).

## Decision

We will configure `ForwardedHeadersOptions` ourselves from a `Proxy`
configuration section and call `UseForwardedHeaders` first in the pipeline:

- `Proxy:KnownNetworks` (CIDRs) and `Proxy:KnownProxies` (addresses) are the
  only callers whose forwarded headers are believed. With neither set, no
  header is forwarded at all.
- `Proxy:ClientIpHeader` names the header carrying the visitor's address:
  `X-Forwarded-For` by default, `CF-Connecting-IP` behind a Cloudflare
  Tunnel only. Behind the orange-cloud proxy and a host proxy, nothing
  overwrites `CF-Connecting-IP` after Cloudflare, so a caller reaching the
  origin directly could forge it; there the host proxy appends
  `X-Forwarded-For` instead.
- `ForwardLimit = 1`: only the hop the trusted proxy added counts.
- The API refuses to start when `ASPNETCORE_FORWARDEDHEADERS_ENABLED` is
  true, since that switch trusts any caller and adds a second pass.
- Compose drops `API_BEHIND_PROXY` for `API_TRUSTED_PROXY_NETWORK`
  (`Proxy__KnownNetworks__0`, empty by default) and `API_CLIENT_IP_HEADER`
  (`Proxy__ClientIpHeader`, default `X-Forwarded-For`).

## Considered alternatives

- **Keep `ASPNETCORE_FORWARDEDHEADERS_ENABLED`** — no trust boundary; one
  open port turns the per-visitor limit into no limit.
- **Read `CF-Connecting-IP` in the rate-limit partition only** — the
  limiter would need its own trust check, logs and every other consumer of
  `RemoteIpAddress` would still see the proxy.
- **Pin the compose network's subnet and trust it by default** — could
  collide with an existing Docker network on the host and would silently
  trust anything else on it; an explicit setting is clearer.

## Consequences

Each visitor gets their own public and join bucket behind any proxy, and a
caller outside the trusted range cannot choose its bucket. `RemoteIpAddress`
is the visitor's for logging as well. The cost is one more setting a
self-hoster must get right: with it empty behind a proxy, everyone shares one
bucket again (safe, but coarse); set too wide, callers inside the range can
forge their address. An existing `.env` with `API_BEHIND_PROXY=true` stops
having any effect and must be migrated.
