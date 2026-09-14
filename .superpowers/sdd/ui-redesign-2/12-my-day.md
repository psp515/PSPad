# Plan 12 — My Day

**Goal.** Redraw `/` against the new shell: overdue pinned in its own section,
then today's tasks, then a collapsed Completed section. Every row is `TaskRow`.

**Spec.** `specs/ui-redesign-2-design.md` D8 (My Day stays flat and unfiltered),
D10 (one task row), D5 (`?task=` opens the panel).

**Architecture.** Blazor WASM page reading the IndexedDB replica through
`IDocumentStore<T>`; commands go out through `CommandSender`. No endpoint, no
module change.

**Constraints.** Owns `src/PSPad.App/Pages/Today.razor` and
`test/PSPad.App.Tests/Pages/TodayTests.cs` and nothing else. `TaskRow` is
read-only. No edit to `wwwroot/css/app.css` — plan 14 owns it. No shared helper
extracted; the toggle/star handlers are duplicated from `ListPage.razor` on
purpose (runbook rule 4).

**Interfaces consumed**

```csharp
TodayRule.Select(IEnumerable<TodoTask> tasks, DateOnly today) -> IReadOnlyList<TodayEntry>
TodayEntry(Guid TaskId, Guid ListId, string Name, bool Overdue, DateOnly? DueOn, bool Recurring, bool Starred, int Priority)
CommandSender.SendAsync(ICommand) -> Task
AppTestHost.Arrange(TestContext, Guid userId, DateOnly today, params Aggregate[]) -> InMemoryReplica
```

`TaskRow` parameters: `Task`, `Today`, `ListName`, `OnToggle`, `OnStar`, `OnOpen`.

**Interfaces produced.** None outside the page.

---

## Task 1 — The three sections, with tests first

**Files**
- modify `test/PSPad.App.Tests/Pages/TodayTests.cs`
- modify `src/PSPad.App/Pages/Today.razor`

The existing test class arranges its own DI by hand; it predates `AppTestHost`.
Replace that arrangement, keep the three behaviours it already covers.

- [ ] Rewrite `TodayTests.cs` onto `AppTestHost`, keeping the existing three
      facts and adding the section ones:

```csharp
using Bunit;
using PSPad.Abstractions;
using PSPad.App.Components;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class TodayTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void OverdueTasksGetTheirOwnSection()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Buy milk", Today.AddDays(-1)));

        var page = Render<Today>();

        Assert.Contains("Overdue", page.Markup);
        Assert.Contains("Buy milk", page.Markup);
    }

    [Fact]
    public void TheOverdueSectionIsAbsentWhenNothingIsOverdue()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Buy milk", Today));

        var page = Render<Today>();

        Assert.DoesNotContain("Overdue", page.Markup);
        Assert.Contains("Buy milk", page.Markup);
    }

    [Fact]
    public void ARecurringTaskMissedYesterdayIsNotOverdue()
    {
        var list = NewList("Regularne");
        Arrange(list, Recurring(list.Id, "Read a book", Today.AddDays(-7)));

        var page = Render<Today>();

        Assert.Contains("Read a book", page.Markup);
        Assert.DoesNotContain("Overdue", page.Markup);
        Assert.DoesNotContain("overdue", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ATaskDueTomorrowIsNotOnTheScreen()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Later", Today.AddDays(1)));

        var page = Render<Today>();

        Assert.DoesNotContain("Later", page.Markup);
    }

    [Fact]
    public void ATaskCompletedTodayMovesToTheCompletedSection()
    {
        var list = NewList("Zakupy");
        var done = Due(list.Id, "Masło", Today);
        done.ApplyAll(TodoTask.Decide(
            done, new CompleteTask(Guid.NewGuid(), User, done.Id),
            new DateTimeOffset(Today.ToDateTime(TimeOnly.Noon), TimeSpan.Zero)));
        Arrange(list, done, Due(list.Id, "Mleko", Today));

        var page = Render<Today>();

        Assert.Contains("Completed (1)", page.Markup);
    }

    [Fact]
    public void EachRowCarriesItsListName()
    {
        var list = NewList("Zakupy");
        Arrange(list, Due(list.Id, "Mleko", Today));

        var page = Render<Today>();

        Assert.Contains("Zakupy", page.Markup);
    }

    [Fact]
    public async Task TogglingATaskCompletesItInTheReplica()
    {
        var list = NewList("Zakupy");
        var task = Due(list.Id, "Mleko", Today);
        var replica = Arrange(list, task);

        var page = Render<Today>();
        page.Find("input.mud-checkbox-input").Change(true);

        var stored = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.NotNull(stored?.CompletedAt);
    }

    InMemoryReplica Arrange(params Aggregate[] documents) =>
        AppTestHost.Arrange(this, User, Today, documents);

    static TaskList NewList(string name)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), name, 0),
            DateTimeOffset.UnixEpoch));
        return list;
    }

    static TodoTask Due(Guid listId, string name, DateOnly due)
    {
        var task = New(listId, name);
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, due), DateTimeOffset.UnixEpoch));
        return task;
    }

    static TodoTask Recurring(Guid listId, string name, DateOnly from)
    {
        var task = New(listId, name);
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, RecurrenceRule.Daily(from)),
            DateTimeOffset.UnixEpoch));
        return task;
    }

    static TodoTask New(Guid listId, string name)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), listId, name),
            DateTimeOffset.UnixEpoch));
        return task;
    }
}
```

- [ ] Run — expect failures, not errors:

```bash
dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"
```

- [ ] Replace `src/PSPad.App/Pages/Today.razor` with:

```razor
@page "/"
@using PSPad.Abstractions
@using PSPad.App.Components
@using PSPad.App.State
@using PSPad.Module.Tasks.Lists
@using PSPad.Module.Tasks.Tasks
@using PSPad.Module.Tasks.Today
@inject IDocumentStore<TodoTask> TaskStore
@inject IDocumentStore<TaskList> ListStore
@inject AppState State
@inject CommandSender Sender
@inject NavigationManager Navigation

<MudText Typo="Typo.h5" Color="Color.Primary" Class="mb-4">My Day</MudText>

@if (_overdue.Count > 0)
{
    <MudText Typo="Typo.subtitle2" Color="Color.Error" Class="mb-2">Overdue</MudText>
    <MudPaper Outlined="true" Class="mb-4">
        @foreach (var task in _overdue)
        {
            <TaskRow Task="@task" Today="@State.Today" ListName="@NameOf(task.ListId)"
                     OnToggle="@ToggleAsync" OnStar="@StarAsync" OnOpen="@Open" />
        }
    </MudPaper>
}

<MudPaper Outlined="true">
    @foreach (var task in _due)
    {
        <TaskRow Task="@task" Today="@State.Today" ListName="@NameOf(task.ListId)"
                 OnToggle="@ToggleAsync" OnStar="@StarAsync" OnOpen="@Open" />
    }

    @if (_overdue.Count == 0 && _due.Count == 0)
    {
        <MudText Typo="Typo.body2" Class="pa-4">Nothing due today.</MudText>
    }
</MudPaper>

@if (_done.Count > 0)
{
    <MudExpansionPanels Elevation="0" Class="mt-4">
        <MudExpansionPanel Text="@($"Completed ({_done.Count})")">
            @foreach (var task in _done)
            {
                <TaskRow Task="@task" Today="@State.Today" ListName="@NameOf(task.ListId)"
                         OnToggle="@ToggleAsync" OnStar="@StarAsync" OnOpen="@Open" />
            }
        </MudExpansionPanel>
    </MudExpansionPanels>
}

@code {
    IReadOnlyList<TodoTask> _overdue = [];
    IReadOnlyList<TodoTask> _due = [];
    IReadOnlyList<TodoTask> _done = [];
    IReadOnlyDictionary<Guid, string> _listNames = new Dictionary<Guid, string>();

    protected override async Task OnInitializedAsync() => await ReloadAsync();

    async Task ReloadAsync()
    {
        var tasks = await TaskStore.LoadAllAsync(State.UserId, CancellationToken.None);
        var lists = await ListStore.LoadAllAsync(State.UserId, CancellationToken.None);

        _listNames = lists.Where(list => !list.Deleted).ToDictionary(list => list.Id, list => list.Name);

        var byId = tasks.ToDictionary(task => task.Id);
        var entries = TodayRule.Select(tasks, State.Today);

        _overdue = [.. entries.Where(entry => entry.Overdue).Select(entry => byId[entry.TaskId])];
        _due = [.. entries.Where(entry => !entry.Overdue).Select(entry => byId[entry.TaskId])];
        _done = [.. tasks.Where(task => !task.Deleted && CompletedToday(task))];
    }

    bool CompletedToday(TodoTask task) =>
        task.CompletedDays.Contains(State.Today)
        || (task.CompletedAt is { } at && DateOnly.FromDateTime(at.UtcDateTime) == State.Today);

    string? NameOf(Guid listId) => _listNames.TryGetValue(listId, out var name) ? name : null;

    void Open(TodoTask task) =>
        Navigation.NavigateTo($"{Navigation.Uri.Split('?')[0]}?task={task.Id}");

    async Task ToggleAsync(TodoTask task)
    {
        if (task.IsRecurring)
        {
            await Sender.SendAsync(new CompleteOccurrence(
                Guid.NewGuid(), State.UserId, task.Id, State.Today, !task.CompletedDays.Contains(State.Today)));
        }
        else if (task.CompletedAt is null)
        {
            await Sender.SendAsync(new CompleteTask(Guid.NewGuid(), State.UserId, task.Id));
        }
        else
        {
            await Sender.SendAsync(new ReopenTask(Guid.NewGuid(), State.UserId, task.Id));
        }

        await ReloadAsync();
    }

    async Task StarAsync(TodoTask task)
    {
        await Sender.SendAsync(new StarTask(Guid.NewGuid(), State.UserId, task.Id, !task.Starred));
        await ReloadAsync();
    }
}
```

- [ ] Run again — all green:

```bash
dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"
```

- [ ] Commit: `feat(app): my day drawn as overdue, today and completed sections`

---

## Task 2 — Clicking a row opens the detail panel

`AppShell` reads `?task=` off the URL, so the page only has to put it there.
Task 1's `Open` already does; this task proves it and pins the route.

**Files**
- modify `test/PSPad.App.Tests/Pages/TodayTests.cs`

- [ ] Add:

```csharp
    [Fact]
    public void ClickingATaskAppendsItToTheQueryString()
    {
        var list = NewList("Zakupy");
        var task = Due(list.Id, "Mleko", Today);
        Arrange(list, task);

        var page = Render<Today>();
        page.Find(".pspad-task-name").Click();

        var navigation = Services.GetRequiredService<NavigationManager>();
        Assert.Contains($"?task={task.Id}", navigation.Uri);
    }
```

Add `using Microsoft.AspNetCore.Components;` and
`using Microsoft.Extensions.DependencyInjection;` at the top.

- [ ] Run:

```bash
dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"
```

- [ ] Commit: `test(app): my day row opens the task panel by query string`

---

## Task 3 — Sidebar counts stay honest after a toggle

`CommandSender.Sent` fires on accepted results and `AppShell` recomputes
`SidebarCounts` from it. Completing a task from My Day must therefore move the
badge without the page doing anything. Nothing to build; prove it, because the
count is the one thing on this screen that lives outside it.

**Files**
- modify `test/PSPad.App.Tests/Pages/TodayTests.cs`

- [ ] Add:

```csharp
    [Fact]
    public async Task CompletingATaskDropsTheTodayCount()
    {
        var list = NewList("Zakupy");
        var task = Due(list.Id, "Mleko", Today);
        Arrange(list, task);

        var counts = new SidebarCounts(
            Services.GetRequiredService<IDocumentStore<TodoTask>>(),
            Services.GetRequiredService<IDocumentStore<Inbox>>(),
            Services.GetRequiredService<AppState>());
        await counts.RefreshAsync();
        Assert.Equal(1, counts.Today);

        var page = Render<Today>();
        page.Find("input.mud-checkbox-input").Change(true);
        await counts.RefreshAsync();

        Assert.Equal(0, counts.Today);
    }
```

Add `using PSPad.Module.Tasks.Inbox;`.

- [ ] Run:

```bash
dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"
```

- [ ] Commit: `test(app): completing from my day lowers the sidebar count`

---

## Done when

- Overdue section renders only when something is overdue, and a recurring task
  never puts it there.
- Rows carry the list name; My Day is cross-area and D10 suppresses the name
  only where the screen already states it.
- Completed section collapses and counts.
- `dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"` green,
  128 + the new facts.
