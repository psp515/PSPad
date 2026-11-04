---
title: A deleted investment account hides its operations without rewriting them
tags: [domain, storage, sync, investments]
date: 2026-11-02
status: Active
---

# ADR-0088: A deleted investment account hides its operations without rewriting them

## Context

Investment accounts could only be archived. A user who imported a file into
an account with the wrong currency had no way to remove it, and the account
kept its slot under the 32-account cap and its broker account number, so the
same file could not be imported again. An account holds up to 20,000
operations; each is its own document, and a delete that rewrote every one
would be 20,000 events, 20,000 synced documents and a command too large for
the outbox to replay safely offline.

## Decision

We will soft-delete the account document only. `DeleteInvestmentAccount`
sets the account's `Deleted` flag and removes it from the portfolio, freeing
its cap slot and its broker account number. Its operations stay as they are;
every read (the client query, the reading layer's cross-account totals and
transfer pairing, the import's target matching, the panels) ignores an
account that is deleted and so every operation that belongs to it.
`InvestmentAccountAccess.Owned` treats a deleted account as missing, so no
write reaches its operations.

## Considered alternatives

- **Cascade the delete to every operation** — up to 20,000 events and
  documents per delete; no bulk-delete path exists in the command pipeline,
  and a partial cascade offline would leave half an account visible.
- **Hard-delete the documents on the server** — sync carries deletions as
  flagged documents, so a removed document would never reach other devices'
  replicas.

## Consequences

- Deleting is one small command, and it syncs like every other deletion.
- Every new read over operations must start from the live accounts; a read
  that loads operations alone would show a deleted account's operations.
- The operations of deleted accounts stay in storage and in each replica.
  They are bounded by the operation cap per account, and a later cleanup
  can remove them without a contract change.
- Re-importing the file into a new account creates fresh operations, since
  imported operation ids derive from the account id.
