# Plan 13 — Inbox

**Goal.** Redraw `/inbox`: a capture field that is always open at the top, and
an item that expands in place into name, area and list with **Move**. No dialog.

**Spec.** `specs/ui-redesign-2-design.md` D9 (organising expands in place), D6
(capture is a permanently open field at the top of the Inbox).

**Architecture.** Blazor WASM page over the replica. `OrganiseInboxItem` already
takes the target list and the new task id, so organising is two commands —
`CreateTask` then `OrganiseInboxItem` — exactly as today's code does. Nothing in
the module changes.

**Constraints.** Owns `src/PSPad.App/Pages/InboxPage.razor` and
`test/PSPad.App.Tests/Pages/InboxPageTests.cs` (new) and nothing else. `TaskRow`
read-only; it does not render here — inbox items are not tasks yet. No
`app.css`. No shared helper.

**Interfaces consumed**

```csharp
Inbox.Items -> IReadOnlyList<InboxItem>              // ordered by Position
InboxItem(Guid Id, string Text, DateTimeOffset CapturedAt, int Position)
CaptureToInbox(Guid CommandId, Guid UserId, Guid InboxId, Guid ItemId, string Text)
OrganiseInboxItem(Guid CommandId, Guid UserId, Guid InboxId, Guid ItemId, Guid ListId, Guid TaskId)
DiscardInboxItem(Guid CommandId, Guid UserId, Guid InboxId, Guid ItemId)
CreateTask(Guid CommandId, Guid UserId, Guid TaskId, Guid ListId, string Name)
```

**Interfaces produced.** None outside the page.

---

## Task 1 — The capture field keeps focus and clears

The current page already captures on Enter. It is kept, moved to the top of the
rewritten markup, and pinned by a test, because the rewrite in task 2 is where
it would quietly be lost.

**Files**
- create `test/PSPad.App.Tests/Pages/InboxPageTests.cs`

- [ ] Write:

```csharp
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using PSPad.Abstractions;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class InboxPageTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public async Task CapturingOnEnterStoresTheItemAndClearsTheField()
    {
        var inbox = NewInbox();
        var replica = Arrange(inbox);

        var page = Render<InboxPage>();
        var input = page.Find("input[placeholder='Capture']");
        input.Input("Zadzwonić do serwisu");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var stored = await replica.LoadAsync<Inbox>(inbox.Id);
        Assert.Contains(stored!.Items, item => item.Text == "Zadzwonić do serwisu");
        Assert.Equal("", page.Find("input[placeholder='Capture']").GetAttribute("value") ?? "");
    }

    [Fact]
    public void CapturedItemsAreListedNewestLast()
    {
        var inbox = NewInbox("Pierwsze", "Drugie");
        Arrange(inbox);

        var page = Render<InboxPage>();

        Assert.True(page.Markup.IndexOf("Pierwsze", StringComparison.Ordinal)
                    < page.Markup.IndexOf("Drugie", StringComparison.Ordinal));
    }

    InMemoryReplica Arrange(params Aggregate[] documents) =>
        AppTestHost.Arrange(this, User, Today, documents);

    static Inbox NewInbox(params string[] texts)
    {
        var inbox = new Inbox();
        inbox.ApplyAll(Inbox.Decide(
            null, new CreateInbox(Guid.NewGuid(), User, Guid.NewGuid()), DateTimeOffset.UnixEpoch));

        foreach (var text in texts)
        {
            inbox.ApplyAll(Inbox.Decide(
                inbox, new CaptureToInbox(Guid.NewGuid(), User, inbox.Id, Guid.NewGuid(), text),
                DateTimeOffset.UnixEpoch));
        }

        return inbox;
    }

    static Area NewArea(string name, int position)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, position),
            DateTimeOffset.UnixEpoch));
        return area;
    }

    static TaskList NewList(Guid areaId, string name)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), areaId, name, 0),
            DateTimeOffset.UnixEpoch));
        return list;
    }
}
```

- [ ] Run — the ordering fact fails against the current page, which orders
      `OrderByDescending(i => i.Position)`:

```bash
dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"
```

- [ ] Commit: `test(app): pin inbox capture and item order`

---

## Task 2 — Organise expands the row in place

**Files**
- modify `test/PSPad.App.Tests/Pages/InboxPageTests.cs`
- modify `src/PSPad.App/Pages/InboxPage.razor`

- [ ] Add the tests:

```csharp
    [Fact]
    public void TappingAnItemOpensNameAreaAndListInsideTheRow()
    {
        var area = NewArea("Dom", 0);
        var list = NewList(area.Id, "Zakupy");
        Arrange(NewInbox("Kupić mleko"), area, list);

        var page = Render<InboxPage>();
        page.Find(".pspad-inbox-item").Click();

        Assert.Contains("Move", page.Markup);
        Assert.Contains("Dom", page.Markup);
    }

    [Fact]
    public async Task MovingCreatesTheTaskInTheChosenListAndOrganisesTheItem()
    {
        var area = NewArea("Dom", 0);
        var list = NewList(area.Id, "Zakupy");
        var inbox = NewInbox("Kupić mleko");
        var replica = Arrange(inbox, area, list);

        var page = Render<InboxPage>();
        page.Find(".pspad-inbox-item").Click();
        page.Find("button.pspad-inbox-move").Click();

        var tasks = await replica.LoadAllAsync<TodoTask>(User);
        Assert.Contains(tasks, task => task.ListId == list.Id && task.Name == "Kupić mleko");

        var stored = await replica.LoadAsync<Inbox>(inbox.Id);
        Assert.Empty(stored!.Items);
    }

    [Fact]
    public async Task DiscardingRemovesTheItemAndCreatesNoTask()
    {
        var inbox = NewInbox("Nieaktualne");
        var replica = Arrange(inbox);

        var page = Render<InboxPage>();
        page.Find("button.pspad-inbox-discard").Click();

        var stored = await replica.LoadAsync<Inbox>(inbox.Id);
        Assert.Empty(stored!.Items);
        Assert.Empty(await replica.LoadAllAsync<TodoTask>(User));
    }
```

- [ ] Replace `src/PSPad.App/Pages/InboxPage.razor` with:

```razor
@page "/inbox"
@using PSPad.Abstractions
@using PSPad.App.State
@using PSPad.Module.Tasks.Areas
@using PSPad.Module.Tasks.Inbox
@using PSPad.Module.Tasks.Lists
@using PSPad.Module.Tasks.Tasks
@inject IDocumentStore<Inbox> InboxStore
@inject IDocumentStore<Area> AreaStore
@inject IDocumentStore<TaskList> ListStore
@inject AppState State
@inject CommandSender Sender

<MudText Typo="Typo.h5" Color="Color.Primary" Class="mb-4">Inbox</MudText>

<MudTextField T="string" @bind-Value="_capture" @ref="_field" Placeholder="Capture"
              Immediate="true" Variant="Variant.Outlined" Margin="Margin.Dense"
              Adornment="Adornment.Start" AdornmentIcon="@Icons.Material.Filled.Add"
              OnKeyDown="@OnCaptureKey" Class="mb-4" />

<MudPaper Outlined="true">
    @foreach (var item in Items)
    {
        <div class="px-3 py-2">
            <div class="d-flex align-center gap-2">
                <div class="flex-grow-1 pspad-inbox-item" style="cursor:pointer"
                     @onclick="@(() => Expand(item))">
                    <MudText Typo="Typo.body2">@item.Text</MudText>
                </div>
                <MudIconButton Class="pspad-inbox-discard" Size="Size.Small"
                               Icon="@Icons.Material.Filled.Delete"
                               OnClick="@(() => DiscardAsync(item))" />
            </div>

            @if (_expanded == item.Id)
            {
                <div class="d-flex flex-column gap-2 mt-2">
                    <MudTextField T="string" @bind-Value="_name" Label="Name" Immediate="true"
                                  Margin="Margin.Dense" Variant="Variant.Outlined" />

                    <MudSelect T="Guid" Value="@_areaId" ValueChanged="@OnAreaChanged" Label="Area"
                               Margin="Margin.Dense" Variant="Variant.Outlined">
                        @foreach (var area in _areas)
                        {
                            <MudSelectItem T="Guid" Value="@area.Id">@area.Name</MudSelectItem>
                        }
                    </MudSelect>

                    <MudSelect T="Guid" @bind-Value="_listId" Label="List"
                               Margin="Margin.Dense" Variant="Variant.Outlined">
                        @foreach (var list in ListsIn(_areaId))
                        {
                            <MudSelectItem T="Guid" Value="@list.Id">@list.Name</MudSelectItem>
                        }
                    </MudSelect>

                    <MudButton Class="pspad-inbox-move" Variant="Variant.Filled" Color="Color.Primary"
                               Disabled="@(_listId == Guid.Empty || string.IsNullOrWhiteSpace(_name))"
                               OnClick="@(() => MoveAsync(item))">
                        Move
                    </MudButton>
                </div>
            }
        </div>

        <MudDivider />
    }

    @if (Items.Count == 0)
    {
        <MudText Typo="Typo.body2" Class="pa-4">Nothing captured.</MudText>
    }
</MudPaper>

@code {
    Inbox? _inbox;
    IReadOnlyList<Area> _areas = [];
    IReadOnlyList<TaskList> _lists = [];

    string _capture = "";
    MudTextField<string>? _field;

    Guid? _expanded;
    string _name = "";
    Guid _areaId;
    Guid _listId;

    IReadOnlyList<InboxItem> Items => _inbox?.Items ?? [];

    protected override async Task OnInitializedAsync() => await ReloadAsync();

    async Task ReloadAsync()
    {
        _inbox = (await InboxStore.LoadAllAsync(State.UserId, CancellationToken.None))
            .FirstOrDefault(inbox => !inbox.Deleted);

        var areas = await AreaStore.LoadAllAsync(State.UserId, CancellationToken.None);
        _areas = [.. areas.Where(area => !area.Deleted).OrderBy(area => area.Position)];

        var lists = await ListStore.LoadAllAsync(State.UserId, CancellationToken.None);
        _lists = [.. lists.Where(list => !list.Deleted).OrderBy(list => list.Position)];
    }

    IReadOnlyList<TaskList> ListsIn(Guid areaId) => [.. _lists.Where(list => list.AreaId == areaId)];

    void Expand(InboxItem item)
    {
        if (_expanded == item.Id)
        {
            _expanded = null;
            return;
        }

        _expanded = item.Id;
        _name = item.Text;
        _areaId = _areas.Count > 0 ? _areas[0].Id : Guid.Empty;
        _listId = ListsIn(_areaId).FirstOrDefault()?.Id ?? Guid.Empty;
    }

    void OnAreaChanged(Guid areaId)
    {
        _areaId = areaId;
        _listId = ListsIn(areaId).FirstOrDefault()?.Id ?? Guid.Empty;
    }

    async Task OnCaptureKey(KeyboardEventArgs args)
    {
        if (args.Key != "Enter" || string.IsNullOrWhiteSpace(_capture) || _inbox is null)
        {
            return;
        }

        await Sender.SendAsync(new CaptureToInbox(
            Guid.NewGuid(), State.UserId, _inbox.Id, Guid.NewGuid(), _capture));

        _capture = "";
        await ReloadAsync();

        if (_field is not null)
        {
            await _field.FocusAsync();
        }
    }

    async Task MoveAsync(InboxItem item)
    {
        if (_inbox is null || _listId == Guid.Empty)
        {
            return;
        }

        var taskId = Guid.NewGuid();
        await Sender.SendAsync(new CreateTask(Guid.NewGuid(), State.UserId, taskId, _listId, _name.Trim()));
        await Sender.SendAsync(new OrganiseInboxItem(
            Guid.NewGuid(), State.UserId, _inbox.Id, item.Id, _listId, taskId));

        _expanded = null;
        await ReloadAsync();
    }

    async Task DiscardAsync(InboxItem item)
    {
        if (_inbox is null)
        {
            return;
        }

        await Sender.SendAsync(new DiscardInboxItem(Guid.NewGuid(), State.UserId, _inbox.Id, item.Id));

        if (_expanded == item.Id)
        {
            _expanded = null;
        }

        await ReloadAsync();
    }
}
```

- [ ] Run — green:

```bash
dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"
```

- [ ] Commit: `feat(app): organise an inbox item in place`

---

## Task 3 — An inbox with no areas or lists cannot strand the user

`Expand` falls back to `Guid.Empty` when the user has no lists, and **Move** is
disabled there. A fresh user is provisioned with seed areas but no lists, so
this is the first state a new account is in, not an edge case.

**Files**
- modify `test/PSPad.App.Tests/Pages/InboxPageTests.cs`

- [ ] Add:

```csharp
    [Fact]
    public void WithNoListsTheMoveButtonIsDisabledRatherThanAbsent()
    {
        Arrange(NewInbox("Kupić mleko"), NewArea("Dom", 0));

        var page = Render<InboxPage>();
        page.Find(".pspad-inbox-item").Click();

        Assert.True(page.Find("button.pspad-inbox-move").HasAttribute("disabled"));
    }
```

- [ ] Run:

```bash
dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"
```

- [ ] Commit: `test(app): inbox move is disabled until a list exists`

---

## Done when

- Capture field sits at the top, commits on Enter, clears and keeps focus.
- Tapping an item expands name, area and list inside the row; **Move** creates
  the task in the chosen list and empties the item out of the inbox.
- Discard removes an item and creates nothing.
- Items render oldest first, matching `Inbox.Items` order.
- `dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"` green.
