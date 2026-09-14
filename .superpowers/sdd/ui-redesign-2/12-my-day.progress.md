# SDD ledger — plan: .superpowers/sdd/ui-redesign-2/12-my-day.md

## Pre-flight scan

Single-track plan (Today.razor + TodayTests.cs only), so no cross-task file/interface
conflicts to check against other plans in this run — only self-consistency below.

| Check | Found |
|---|---|
| Task 1 test setup vs AppTestHost | Uses AppTestHost.Arrange; AppTestHost registers IDocumentStore<TodoTask/TaskList/Inbox/Area/Goal>, AppState, CommandSender, CardCollapseState — all match Today.razor's @inject list. Clean. |
| Task 1 CompletedToday() vs test's CompleteTask timestamp | Test sets DateTimeOffset to Today at noon; CompletedToday checks CompletedDays.Contains(Today) OR DateOnly.FromDateTime(CompletedAt) == Today. Clean. |
| Task 2 .pspad-task-name selector vs TaskRow.razor | TaskRow.razor:8 has exactly that class on the clickable name div. Clean. |
| Task 2 NavigationManager via Services.GetRequiredService | Bunit TestContext registers a fake NavigationManager by default. Clean. |
| Task 3 SidebarCounts ctor args vs AppTestHost registrations | IDocumentStore<TodoTask>, IDocumentStore<Inbox>, AppState all registered by AppTestHost. Clean. |
| Task/global-constraint self-consistency | Toggle/star handlers duplicated from ListPage.razor per runbook rule 4 (deliberate, not extracted). TaskRow untouched. No app.css touch specified. Clean. |

Scan clean. No rulings needed. Proceeding to Task 1.

Task 1: complete (commits 0a2704d..2e770d8, review clean)

Task 2: complete (commits 2e770d8..2389635, review clean)

Task 3: complete (commits 2389635..751a801, review clean)

## Final whole-branch review (opus, 0a2704d..751a801)

Plan alignment: match. 134/134 unit tests green. Findings:
- Important: Today.razor:79 CompletedToday dates completion in UTC not user's zone (AGENTS.md §3 violation) — a just-completed task can vanish from My Day for hours. FIX REQUIRED.
- Minor: empty outlined MudPaper when overdue>0 and due==0 (stray box) — FIX.
- Minor: _done unordered, reshuffles between reloads — FIX (order by CompletedAt desc, then name).
- Minor: weak assertion in ATaskCompletedTodayMovesToTheCompletedSection (only checks count, not placement) — FIX.
- Minor (product judgement, not defect): completed-but-not-due-today tasks appear in My Day's Completed section. Ruling: parked — spec D8 doesn't settle this; leaving as-is, matches "flat and unfiltered" framing (My Day shows what's done today regardless of due date, symmetric with showing overdue regardless of when due). Cost if wrong: a task due next week completed early shows briefly in My Day's Completed panel until next day — cosmetic, reversible, no data risk.
- Minor (deferred): no test for recurring-toggle branch, StarAsync, or "Nothing due today" empty state. Ruling: parked — not required by plan scope, adding now is scope creep on this fix-wave. Cost if wrong: those lines stay untested; low risk, pure UI branches already exercised by the same code paths in ListPage.razor's own tests.

Dispatching ONE fix subagent for: Important (UTC bug) + 3 Minor code fixes (empty paper, ordering, weak assertion).

Final review fix wave: complete (commits 751a801..b02642e, all 4 findings ADDRESSED, no new breakage, 135/135 tests). Plan 12 COMPLETE.
