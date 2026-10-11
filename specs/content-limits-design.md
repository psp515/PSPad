# Content limits — design

Status: Built. Issue #126 (OWASP A04), part of #118.

## 1. Problem

`Decide` rejected only blank names. Nothing bounded the length of a name,
label, field value or description, nor how many steps, fields or members an
aggregate holds. A member of a shared list could send multi-MB descriptions
or thousands of steps; the text lands twice (document and event) under the
owner's `UserId`, in the owner's sync and replica, and once the document nears
MongoDB's 16 MB limit every later command on it fails with a 500.

## 2. Decision

Limits are domain rules, checked in `Decide`, so the same check runs in WASM
and on the server (`adr/0003`, `adr/0004`). They live once, as constants on
`PSPad.Module.Tasks.ContentLimits`, which the client reads for its inputs.

| Limit | Value | Applies to |
|---|---|---|
| `Name` | 64 | area, list, goal, task, step, reference item, inbox item text |
| `Label` | 64 | reference field label |
| `FieldValue` | 1,024 | reference field value |
| `Description` | 8,192 | task and reference item description (Markdown) |
| `StepsPerTask` | 128 | `AddStep` |
| `FieldsPerItem` | 64 | `AddReferenceField` |
| `MembersPerList` | 100 | `JoinTaskList` |

- Lengths count UTF-16 code units after the existing trim — what
  `string.Length` and the HTML `maxlength` attribute both count.
- Over a limit is a `DomainRejectedException`; the outbox surfaces it like any
  other rejection.
- Inbox item text shares `Name` because organising an item makes it a task
  name.
- A member's display name and the owner name on a share come from the
  identity provider, not from typing. They are **truncated** to `Name`, never
  rejected, so a long Keycloak name cannot lock someone out of joining.
- A full list rejects a join; the join endpoint answers its usual `404`.
- Limits apply to writes only. A document already over a limit still loads;
  adding one more step or field to it is rejected, editing it down is not.

## 3. Client

- Every name, label, value and description input sets `MaxLength` from
  `ContentLimits`; the description editor shows a counter.
- `SyncService` keeps its 50-command batch but also stops a batch once its
  serialized envelopes pass 512 KB (always sending at least one), so a
  batch always fits the server's body limit.

## 4. Server

- `KestrelServerOptions.Limits.MaxRequestBodySize` is 1 MB (default 30 MB).
  The largest single command — an 8,192-character description with every
  character JSON-escaped — stays well under it.

## 5. Out of scope

- Inbox item count. The Inbox is never shared; a user can only fill their own.
- Per-user aggregate counts (areas, lists, tasks). Each lives in its own
  document, so none can push another past 16 MB.
