# SDD ledger — plan: .superpowers/sdd/ui-redesign-2/13-inbox.md

Reimplemented directly on `feature/psp/inbox` (branched from `main`), not through
a fresh pre-flight scan against sibling tracks — 12 already merged-in-spirit on
its own branch, 14/15/16 still unstarted, so no sibling-file collision to check.

Tasks 1–3 done in one TDD pass: wrote the full test file (all 7 facts from the
plan) against the current `InboxPage.razor`, confirmed six failures for the
right reasons (missing markup/selectors, wrong item order), then rewrote the
page per the plan.

Two real defects surfaced that the plan text did not anticipate:

- **Important, caught by the test suite itself.** The plan's `MoveAsync` (and
  the pre-existing page it was replacing) sends `CreateTask` then
  `OrganiseInboxItem` as two commands. `OrganiseInboxItemHandler` already
  creates the task atomically from the item's text when it applies
  `OrganiseInboxItem` (true since plan 02 — one commit, unchanged). Sending a
  separate `CreateTask` first makes the handler's own internal `CreateTask`
  collide with the one just committed and get silently rejected — the item
  never actually leaves the inbox. This has been live in production since
  plan 02; there was simply no test exercising Organise before this plan.
  Fixed by sending only `OrganiseInboxItem`. Consequence: the in-row name
  can no longer be an editable field (the command has no slot for a renamed
  task), so it is a read-only label instead of a `MudTextField` — editing it
  would have silently done nothing.
- **Important, caught only by driving the real page in a browser (bUnit's
  markup-string assertions missed it).** When the expanded row's area has no
  lists, the List `MudSelect`'s visible box showed the literal string
  `00000000-0000-0000-0000-000000000000` instead of anything readable — bUnit
  markup happened to always contain that string too (in a hidden input's
  `value` attribute), so a naive `DoesNotContain` assertion on full markup is
  not the right test for this; the fix is verified by what's visibly rendered
  in the browser screenshot, and by asserting the placeholder text is present.
  Fixed with a placeholder `MudSelectItem` for `Guid.Empty` when the picked
  area has no lists.

Verified end-to-end in a browser (docker compose stack, Keycloak `demo`/`demo`
user): captured two items, expanded one, picked an area with no lists (showed
the placeholder correctly, Move stayed disabled), switched to an area with a
list (Move enabled), moved it — item left the inbox, task landed in the right
list, sidebar Inbox badge dropped 2 → 1.

134/134 `PSPad.App.Tests`, 81+6+6 module unit tests, full solution build clean.

Plan 13 COMPLETE. Two commits on `feature/psp/inbox`:
`feat(app): organise an inbox item in place`,
`fix(app): show a label instead of a raw guid for an empty list select`.
