---
title: Rendered Markdown makes no automatic requests; images show as links
tags: [ui, security, privacy, sharing]
date: 2026-10-13
status: Active
---

# ADR-0068: Rendered Markdown makes no automatic requests; images show as links

> Amends [ADR-0048](0048-descriptions-are-markdown.md).

## Context

ADR-0048 renders descriptions with Markdig and filters link schemes, but
images with `http`/`https` sources still rendered as `<img>`. Descriptions are
shown to list members and, through public snapshots, to anonymous visitors.
One `![](https://host/p.gif?u=…)` turns every view into a tracking pixel that
leaks the viewer's IP address, user agent and viewing time; plain `http`
images also load as mixed content behind TLS (GitHub issue #136).

## Decision

We will never let rendered Markdown make the browser fetch anything on its
own. Markdig keeps `DisableHtml()`, so raw HTML (`iframe`, `video`, `object`,
`link`, `style`, `srcset`, inline `style`) is escaped as text. An image
`![alt](url)` renders as a plain link to `url` labelled with its alt text (the
URL itself when the alt text is empty), under the same `http`/`https`/`mailto`
filter and `target="_blank" rel="noopener noreferrer"` as any link. An image
with any other scheme, `data:` included, shows its alt text only. An image
inside a link shows its alt text as that link's label. No `<img>` is ever
emitted. Clicking a link stays user-initiated navigation and is allowed.

## Considered alternatives

- **Allow `https` images only, with `referrerpolicy="no-referrer"` and
  `loading="lazy"`** — the request still goes out and still reveals the
  viewer's IP address and viewing time.
- **Proxy images through the API** — hides the viewer but costs a server-side
  fetcher (SSRF surface, bandwidth) for no use case that needs inline images.
- **Drop images silently, or show only the alt text** — loses the URL the
  author meant to share.

## Consequences

Viewing a description, a shared list or a public snapshot contacts nobody
but PSPad. Authors who want to show a picture get a link instead of an inline
image. A future rich-media feature (attachments) needs its own decision. The
rule lives in one place, `MarkdownRenderer`, and is covered by its tests.
