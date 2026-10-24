# Step deadlines — design

Status: Designed, not built. Issue #202. Mockup: artifact "Step Deadlines"
(https://claude.ai/artifact/NtP3h8xfqfNy5DnBNndFAy). Decision:
`adr/0078-any-unchecked-step-deadline-puts-its-task-on-a-day.md`.

## 1. Problem

A step can already carry a date: `Step.DueOn` exists, `SetStepDueDate` /
`StepDueDateSet` set and clear it, and the document stores it inline in
`_steps`. Nothing in the client sends the command, so no user can set one.

The Today rule also reads only the **next** unchecked step's date
(`TodoTask.NextUncheckedStep`, then `TodayRule.EarliestTrigger`). A deadline
on step 3 is ignored until steps 1 and 2 are checked, so a task does not show
on the day its step is due, which is what the issue asks for.

The card gives no sign that a step, not the task, put it on the day.

## 2. Decisions

### 2.1 The rule: the earliest unchecked step deadline

- `TodoTask.EarliestDueStep` (derived, not serialized): among unchecked
  steps with a `DueOn`, the one with the earliest date. A tie goes to the
  lower `Position`. `null` when no unchecked step has a date.
- `TodayRule.EarliestTrigger(task)` becomes min(`task.DueOn`,
  `task.EarliestDueStep?.DueOn`), either side may be `null`. It is made
  public as `TodayRule.TriggerOf(task)` so list sort, filter and calendar
  (#198) read the same date. Nothing else in `TodayRule` changes: Overdue is
  still `trigger < today`, Coming up and lead time use the trigger, Starred
  compares against it.
- Repeating tasks ignore step dates, as today: their days come from
  `OccursOn`, and a repeat is never overdue.
- A step date may fall after the task's own due date. `Decide` does not
  validate the pair; the earlier one wins.
- Checking a step drops its date from the rule. The task moves to the next
  dated step's day, or back to its own due date, or off every day.
- `TodoTask.NextUncheckedStep` had the rule as its only reader and is
  removed; `EarliestDueStep` replaces it.

No command, event, field, endpoint or document change. `GET /api/today`
answers from the same rule and changes only in which tasks it returns.

### 2.2 Setting a deadline (`StepList`)

- Each unchecked step row, between the name and the remove ✕, ends with a
  date control (`pspad-step-due`):
  - **Set**: a small text button reading the date relatively (Today,
    Tomorrow, Yesterday, else `d MMM`, the same wording as
    `DueDateRow.Describe`), in `Color.Error` when the date is before today.
  - **Empty**: a muted `Icons.Material.Outlined.Event` icon button with the
    accessible label "Set a deadline for {step}". It uses the existing
    `pspad-step-action` opacity rule, so it shows on hover on desktop and
    always on touch.
- Both open one `MudMenu` holding the Due row's choices: Today / Tomorrow /
  In 2 days / Next week (each with its `ddd, d MMM` date), a divider,
  "Pick a date…" (the same static `MudDatePicker` dialog), then "Clear date"
  when a date is set. Choosing sends `SetStepDueDate` with the day or
  `null`; choosing the current value sends nothing.
- The quick picks and the picker dialog move out of `DueDateRow` into a new
  `Components/DueDateMenu.razor`, which `DueDateRow` and `StepList` both use.
  `DueDateRow.QuickPicks` and `Describe` stay where callers find them.
- A checked step shows no date control: its date no longer counts. The date
  stays stored and returns if the step is unchecked.
- On a repeating task (`TodoTask.IsRecurring`) no step shows the control.
  Dates already stored stay and are ignored.
- In View mode (`Disabled`) the set date reads as plain text and the empty
  control is hidden.

### 2.3 Deadlines while creating

`StepList.Draft` becomes `List<StepDraft>`, a new client record
`StepDraft(string Name, DateOnly? DueOn)` in `PSPad.App/State` (mutable
`DueOn`, or replaced in place). Draft rows get the same date control and send
nothing. On save:

- `TaskDetailPanel` Add (#201) and `InboxItemPanel` convert send, per draft
  step in order, `AddStep` and then `SetStepDueDate` when the draft has a
  date.
- The Inbox spec's "Draft steps have no checkbox and no due date" becomes
  "no checkbox".

### 2.4 The card mark (`TaskRow`)

When `Task.EarliestDueStep` is not `null` and the task is not repeating, the
second line shows `pspad-step-due-mark` right after the task's own due date:
`Icons.Material.Outlined.SubdirectoryArrowRight`, the step's name, " · ",
then the step's date as `d MMM`. The step name shrinks with an ellipsis like
the list name; the icon and date stay whole. The whole mark is in
`Color.Error` when the step's date is before today. The full step name is
the mark's `title`. `HasMeta` counts it.

It shows on every screen that renders `TaskRow`, the goal screen included.
Both the task's due date and the mark show when both are set.

## 3. Out of scope

- Step deadlines on repeating tasks (per-occurrence steps do not exist).
- Reminders or notifications for a step deadline.
- A step date in the public snapshot view.
- Showing the step mark in My Day's schedule timeline blocks.

## 4. Tests

The rule is tested in three places on purpose (`backend-spec.md` §4):

- **Module unit** (`Today/TodayRuleTests`, `Today/DayPlanTests`,
  `Today/DayViewPlanTests`, `Today/LeadTimePlanTests`): a later step's
  date triggers; a checked step's date does not; an earlier task due date
  wins; a tie picks the lower position; a past step date is Overdue; a
  repeating task ignores step dates; `TriggerOf` matches Plan.
- **API integration** (`Endpoints/TodayEndpointTests`): a task whose third step is
  due today is returned by `GET /api/today`.
- **Client bUnit** (`Pages/TodayTests`): the same task shows on My
  Day for that day, and in Coming up from the day before.

Plus bUnit for `DueDateMenu`, the `StepList` date control (sends
`SetStepDueDate`, Clear sends `null`, hidden on checked steps, repeating tasks
and View), draft dates in `TaskDetailPanel` and `InboxItemPanel`, and the
`TaskRow` mark; an E2E scenario `21-step-deadline.spec.ts`.
