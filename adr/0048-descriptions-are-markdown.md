---
title: Descriptions are Markdown, rendered client-side with raw HTML disabled
tags: [domain, ui, security]
date: 2026-09-28
status: Proposed
---

# ADR-0048: Descriptions are Markdown, rendered client-side with raw HTML disabled

## Context

Tasks and reference items need a long free-text description. GitHub issue
bodies, mirrored later, are Markdown. The client is an offline WebAssembly
app; descriptions are user- or GitHub-authored and rendered into the DOM.

## Decision

We will store descriptions as plain strings holding Markdown and render them
in the client with Markdig, `DisableHtml()` on, links limited to `http`,
`https` and `mailto`, task-list and table extensions on with checkboxes
disabled. The shared `MarkdownField` shows the rendered view by default and
switches to a raw editor on demand.

## Considered alternatives

- **Plain text with auto-linked URLs** — mirrored GitHub bodies would show
  raw `##` and `- [ ]` noise.
- **A rich-text (WYSIWYG) editor** — heavy JS dependency, stores HTML, and
  editing offline in WASM gains nothing Markdown does not already give.
- **Rendering on the server** — the client must render offline.
- **Markdig with an HTML sanitizer instead of `DisableHtml()`** — a second
  dependency to keep correct; no use case needs raw HTML.

## Consequences

Descriptions render the same on and offline, and GitHub bodies display as
intended. Raw HTML in a description shows as text. Markdig ships in the WASM
payload (small). Scheme filtering is one custom rule to test.
