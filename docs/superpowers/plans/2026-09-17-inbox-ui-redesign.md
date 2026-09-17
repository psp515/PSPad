# Inbox UI Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the Inbox screen — each captured item becomes its own card
like Area's `ListCard`, capture moves from an inline text field to a FAB with
a quick-add dialog, and tapping an item opens a right-side panel (full width
on small screens) to move it into an area/list or discard it. Also fixes
`TaskDetailPanel`'s drawer to be full-width on small screens, which it
currently is not despite the spec describing that behaviour.

**Architecture:** Three small, focused components replace `InboxPage`'s
current inline markup: `InboxItemCard` (one item, one card, click opens the
panel), `CaptureDialog` (an `IDialogService` dialog matching the existing
`NameDialog`/`AddTaskDialog` pattern), and `InboxItemPanel` (a `MudDrawer`
bound directly to a nullable parameter, following the existing
`TaskDetailPanel` pattern rather than `IDialogService` — it needs no
deep-linking, so it stays local to `InboxPage`). Both drawers (the new
`InboxItemPanel` and the existing `TaskDetailPanel`) get their responsive
width from the app's existing `IViewport` abstraction (already used by
`AppShell` for nav-drawer swap), not a CSS media query — matching the
project's established pattern and avoiding a second way to detect viewport
size.

**Tech Stack:** Blazor WebAssembly, MudBlazor, bUnit (no mocking framework —
hand-written `sealed class` test doubles per house style).

**Spec:** No dedicated spec file — this is a bounded UI change to an existing
screen, decided in conversation. Relevant background: `specs/ui-redesign-2-design.md`
(task detail overlay, card-based screens) and `specs/ui-polish-design.md`.

## Global Constraints

- TDD: failing test first, every task.
- Every test class carries `[UnitTest]` (from `PSPad.TestInfrastructure`).
- No comments in code except a genuinely counter-intuitive constraint.
- One type per file, file-scoped namespaces, Allman braces, 4-space indent.
- No mocking framework — test doubles are small hand-written `sealed class`
  implementations of the relevant interface (see existing
  `NeverLoadingInboxStore`, `AppTestHost.FakeViewport`).
- CSS hooks use a `.pspad-` prefix; MudBlazor's own classes
  (`.mud-menu-item`, `.mud-paper`, `.mud-drawer`) are used directly where
  they're the thing under test.
- Dialogs shown via `IDialogService` are tested through their host page (see
  `AreaBoardTests`'s `AddTaskDialog` coverage), not with a standalone test
  file — `CaptureDialog` follows that precedent. Components bound directly to
  a parameter (not shown via `IDialogService`) get their own dedicated test
  file — see `TaskDetailPanelTests` — and `InboxItemPanel` follows that
  precedent instead.
- Run tests with `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~<ClassName>"`
  per task, and `dotnet test --filter Category=Unit` before the final commit.

---

### Task 1: `TaskDetailPanel` — responsive drawer width

**Files:**
- Modify: `src/PSPad.App/Layout/TaskDetailPanel.razor`
- Test: `test/PSPad.App.Tests/Layout/TaskDetailPanelTests.cs`

**Interfaces:**
- Consumes: `PSPad.App.State.IViewport` (`Task SubscribeAsync(Action<bool> onDesktopChanged)`,
  `ValueTask DisposeAsync()`), already registered in DI and faked in tests via
  `AppTestHost.FakeViewport(bool isDesktop)`.
- Produces: no change to `TaskDetailPanel`'s public parameters
  (`TaskId`, `Disabled`, `OnClose`) — only its rendered `Width`.

- [ ] **Step 1: Write the failing tests**

Add to `test/PSPad.App.Tests/Layout/TaskDetailPanelTests.cs` (after the
existing tests, before the `static` helper methods):

```csharp
    [Fact]
    public void OnADesktopViewportTheDrawerKeepsItsFixedColumnWidth()
    {
        var task = NewTask("Buy milk");
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        Assert.Contains("360px", panel.Find(".mud-drawer").GetAttribute("style"));
    }

    [Fact]
    public void OnASmallViewportTheDrawerFillsTheFullWidthInsteadOfAFixedColumn()
    {
        var task = NewTask("Buy milk");
        AppTestHost.Arrange(this, User, Today, task);
        Services.AddSingleton<IViewport>(new AppTestHost.FakeViewport(isDesktop: false));

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        Assert.Contains("100%", panel.Find(".mud-drawer").GetAttribute("style"));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~TaskDetailPanelTests"`
Expected: the two new tests FAIL — the drawer always renders `360px`, so the
mobile-viewport test fails; the desktop test may pass already (fine, it's a
regression guard, not new behaviour).

- [ ] **Step 3: Add the responsive width**

In `src/PSPad.App/Layout/TaskDetailPanel.razor`, add the injection and the
`IAsyncDisposable` implementation next to the existing ones at the top:

```razor
@inject IViewport Viewport
@implements IAsyncDisposable
```

Change the drawer's `Width` attribute from:

```razor
<MudDrawer Open="@(TaskId is not null)" OpenChanged="@OnOpenChanged" Anchor="Anchor.Right"
           Variant="DrawerVariant.Temporary" Elevation="6" Width="360px" OverlayAutoClose="true">
```

to:

```razor
<MudDrawer Open="@(TaskId is not null)" OpenChanged="@OnOpenChanged" Anchor="Anchor.Right"
           Variant="DrawerVariant.Temporary" Elevation="6" Width="@(_isDesktop ? "360px" : "100%")"
           OverlayAutoClose="true">
```

In the `@code` block, add a field next to the other private fields:

```csharp
    bool _isDesktop = true;
```

Add an `OnInitializedAsync` override (the component currently only has
`OnParametersSetAsync`) near the top of `@code`:

```csharp
    protected override async Task OnInitializedAsync() => await Viewport.SubscribeAsync(OnDesktopChanged);
```

Add the handler and the dispose method near the other small methods
(`OnOpenChanged` etc.):

```csharp
    void OnDesktopChanged(bool isDesktop)
    {
        _isDesktop = isDesktop;
        InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync() => await Viewport.DisposeAsync();
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~TaskDetailPanelTests"`
Expected: PASS, all tests including the two new ones.

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.App/Layout/TaskDetailPanel.razor test/PSPad.App.Tests/Layout/TaskDetailPanelTests.cs
git commit -m "fix: make the task detail drawer full width on small screens"
```

---

### Task 2: `InboxItemCard` component

**Files:**
- Create: `src/PSPad.App/Components/InboxItemCard.razor`
- Test: `test/PSPad.App.Tests/Components/InboxItemCardTests.cs`

**Interfaces:**
- Consumes: `PSPad.Module.Tasks.Inbox.InboxItem` (has `Id`, `Text`).
- Produces: `InboxItemCard` with parameters `Item` (`InboxItem`,
  `EditorRequired`) and `OnOpen` (`EventCallback<InboxItem>`). Root element
  is a `MudPaper Outlined="true"`; the clickable inner element carries class
  `pspad-inbox-item`. Task 4 (`InboxPage`) renders one of these per item and
  wires `OnOpen` to open `InboxItemPanel`.

- [ ] **Step 1: Write the failing test**

Create `test/PSPad.App.Tests/Components/InboxItemCardTests.cs`:

```csharp
using Bunit;
using Microsoft.AspNetCore.Components;
using PSPad.App.Components;
using PSPad.Module.Tasks.Inbox;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class InboxItemCardTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void ItIsItsOwnOutlinedCard()
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
        var item = NewItem("Kupić mleko");

        var card = Render<InboxItemCard>(parameters => parameters.Add(p => p.Item, item));

        var classes = card.Find(".mud-paper").ClassList;
        Assert.Contains("mud-paper-outlined", classes);
        Assert.Contains("Kupić mleko", card.Markup);
    }

    [Fact]
    public void ClickingItRaisesOnOpenWithTheItem()
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
        var item = NewItem("Kupić mleko");
        InboxItem? opened = null;

        var card = Render<InboxItemCard>(parameters => parameters
            .Add(p => p.Item, item)
            .Add(p => p.OnOpen, EventCallback.Factory.Create<InboxItem>(this, i => opened = i)));

        card.Find(".pspad-inbox-item").Click();

        Assert.Equal(item.Id, opened?.Id);
    }

    static InboxItem NewItem(string text)
    {
        var inbox = new Inbox();
        inbox.ApplyAll(Inbox.Decide(
            null, new CreateInbox(Guid.NewGuid(), User, Guid.NewGuid()), DateTimeOffset.UnixEpoch));
        inbox.ApplyAll(Inbox.Decide(
            inbox, new CaptureToInbox(Guid.NewGuid(), User, inbox.Id, Guid.NewGuid(), text),
            DateTimeOffset.UnixEpoch));
        return inbox.Items[0];
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~InboxItemCardTests"`
Expected: FAIL to compile — `InboxItemCard` doesn't exist yet.

- [ ] **Step 3: Write the component**

Create `src/PSPad.App/Components/InboxItemCard.razor`:

```razor
@using PSPad.Module.Tasks.Inbox

<MudPaper Outlined="true">
    <div class="pspad-inbox-item pa-3" style="cursor:pointer" @onclick="@(() => OnOpen.InvokeAsync(Item))">
        <MudText Typo="Typo.body2">@Item.Text</MudText>
    </div>
</MudPaper>

@code {
    [Parameter, EditorRequired] public InboxItem Item { get; set; } = null!;

    [Parameter] public EventCallback<InboxItem> OnOpen { get; set; }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~InboxItemCardTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.App/Components/InboxItemCard.razor test/PSPad.App.Tests/Components/InboxItemCardTests.cs
git commit -m "feat: add InboxItemCard for the card-based inbox list"
```

---

### Task 3: `InboxItemPanel` component

**Files:**
- Create: `src/PSPad.App/Components/InboxItemPanel.razor`
- Test: `test/PSPad.App.Tests/Components/InboxItemPanelTests.cs`

**Interfaces:**
- Consumes: `IDocumentStore<Area>`, `IDocumentStore<TaskList>`, `AppState`,
  `CommandSender`, `IViewport` (all already registered in DI/`AppTestHost`).
  Sends `PSPad.Module.Tasks.Inbox.OrganiseInboxItem(Guid commandId, Guid
  userId, Guid inboxId, Guid itemId, Guid listId, Guid taskId)` and
  `DiscardInboxItem(Guid commandId, Guid userId, Guid inboxId, Guid itemId)`
  — both already exist, unchanged, used today by `InboxPage`.
- Produces: `InboxItemPanel` with parameters `Item` (`InboxItem?`),
  `InboxId` (`Guid`), `OnClose` (`EventCallback`), `OnChanged`
  (`EventCallback`, raised after a successful Move or Discard so the host
  page can reload). Renders a `MudDrawer` with class-selectable buttons
  `button.pspad-inbox-move`, `button.pspad-inbox-discard`, and close icon
  `.pspad-inbox-panel-close`. Task 4 (`InboxPage`) renders exactly one of
  these, always in the tree, driven by a local `InboxItem? _openItem` field.

- [ ] **Step 1: Write the failing tests**

Create `test/PSPad.App.Tests/Components/InboxItemPanelTests.cs`:

```csharp
using Bunit;
using PSPad.Abstractions;
using PSPad.App.Components;
using PSPad.App.State;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class InboxItemPanelTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void WithNoItemItShowsNothing()
    {
        AppTestHost.Arrange(this, User, Today);

        var panel = Render<InboxItemPanel>(parameters => parameters
            .Add(p => p.Item, (InboxItem?)null)
            .Add(p => p.InboxId, Guid.NewGuid()));

        Assert.DoesNotContain("Move", panel.Markup);
    }

    [Fact]
    public void WithAnItemItShowsItsTextAndTheFirstAreaAndListByDefault()
    {
        var area = NewArea("Dom", 0);
        var list = NewList(area.Id, "Zakupy");
        var inbox = NewInbox("Kupić mleko");
        AppTestHost.Arrange(this, User, Today, inbox, area, list);

        var panel = Render<InboxItemPanel>(parameters => parameters
            .Add(p => p.Item, inbox.Items[0])
            .Add(p => p.InboxId, inbox.Id));

        Assert.Contains("Kupić mleko", panel.Markup);
        Assert.Contains("Dom", panel.Markup);
    }

    [Fact]
    public async Task MovingCreatesTheTaskInTheChosenListAndOrganisesTheItem()
    {
        var area = NewArea("Dom", 0);
        var list = NewList(area.Id, "Zakupy");
        var inbox = NewInbox("Kupić mleko");
        var replica = AppTestHost.Arrange(this, User, Today, inbox, area, list);

        var panel = Render<InboxItemPanel>(parameters => parameters
            .Add(p => p.Item, inbox.Items[0])
            .Add(p => p.InboxId, inbox.Id));
        panel.Find("button.pspad-inbox-move").Click();

        var tasks = await replica.LoadAllAsync<TodoTask>(User);
        Assert.Contains(tasks, task => task.ListId == list.Id && task.Name == "Kupić mleko");

        var stored = await replica.LoadAsync<Inbox>(inbox.Id);
        Assert.Empty(stored!.Items);
    }

    [Fact]
    public async Task DiscardingRemovesTheItemAndCreatesNoTask()
    {
        var inbox = NewInbox("Nieaktualne");
        var replica = AppTestHost.Arrange(this, User, Today, inbox);

        var panel = Render<InboxItemPanel>(parameters => parameters
            .Add(p => p.Item, inbox.Items[0])
            .Add(p => p.InboxId, inbox.Id));
        panel.Find("button.pspad-inbox-discard").Click();

        var stored = await replica.LoadAsync<Inbox>(inbox.Id);
        Assert.Empty(stored!.Items);
        Assert.Empty(await replica.LoadAllAsync<TodoTask>(User));
    }

    [Fact]
    public void ClosingInvokesOnClose()
    {
        var inbox = NewInbox("Kupić mleko");
        AppTestHost.Arrange(this, User, Today, inbox);
        var closed = false;

        var panel = Render<InboxItemPanel>(parameters => parameters
            .Add(p => p.Item, inbox.Items[0])
            .Add(p => p.InboxId, inbox.Id)
            .Add(p => p.OnClose, () => closed = true));
        panel.Find(".pspad-inbox-panel-close").Click();

        Assert.True(closed);
    }

    [Fact]
    public void WithNoListsInTheAreaTheMoveButtonIsDisabledRatherThanAbsent()
    {
        var area = NewArea("Dom", 0);
        var inbox = NewInbox("Kupić mleko");
        AppTestHost.Arrange(this, User, Today, inbox, area);

        var panel = Render<InboxItemPanel>(parameters => parameters
            .Add(p => p.Item, inbox.Items[0])
            .Add(p => p.InboxId, inbox.Id));

        Assert.True(panel.Find("button.pspad-inbox-move").HasAttribute("disabled"));
        Assert.Contains("No lists in this area", panel.Markup);
    }

    [Fact]
    public void OnASmallViewportTheDrawerFillsTheFullWidthInsteadOfAFixedColumn()
    {
        var inbox = NewInbox("Kupić mleko");
        AppTestHost.Arrange(this, User, Today, inbox);
        Services.AddSingleton<IViewport>(new AppTestHost.FakeViewport(isDesktop: false));

        var panel = Render<InboxItemPanel>(parameters => parameters
            .Add(p => p.Item, inbox.Items[0])
            .Add(p => p.InboxId, inbox.Id));

        Assert.Contains("100%", panel.Find(".mud-drawer").GetAttribute("style"));
    }

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

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~InboxItemPanelTests"`
Expected: FAIL to compile — `InboxItemPanel` doesn't exist yet.

- [ ] **Step 3: Write the component**

Create `src/PSPad.App/Components/InboxItemPanel.razor`:

```razor
@using PSPad.Abstractions
@using PSPad.App.State
@using PSPad.Module.Tasks.Areas
@using PSPad.Module.Tasks.Inbox
@using PSPad.Module.Tasks.Lists
@inject IDocumentStore<Area> AreaStore
@inject IDocumentStore<TaskList> ListStore
@inject AppState State
@inject CommandSender Sender
@inject IViewport Viewport
@implements IAsyncDisposable

<MudDrawer Open="@(Item is not null)" OpenChanged="@OnOpenChanged" Anchor="Anchor.Right"
           Variant="DrawerVariant.Temporary" Elevation="6" Width="@(_isDesktop ? "360px" : "100%")"
           OverlayAutoClose="true">
    @if (Item is not null)
    {
        <div class="d-flex align-center gap-2 pa-3">
            <MudText Typo="Typo.subtitle2" Class="flex-grow-1">@Item.Text</MudText>
            <MudIconButton Class="pspad-inbox-panel-close" Size="Size.Small" Icon="@Icons.Material.Filled.Close"
                           OnClick="@OnClose" />
        </div>

        <MudDivider />

        <div class="pa-3 d-flex flex-column gap-3">
            <MudSelect T="Guid" Value="@_areaId" ValueChanged="@OnAreaChanged" Label="Area"
                       Margin="Margin.Dense" Variant="Variant.Outlined">
                @foreach (var area in _areas)
                {
                    <MudSelectItem T="Guid" Value="@area.Id">@area.Name</MudSelectItem>
                }
            </MudSelect>

            <MudSelect T="Guid" @bind-Value="_listId" Label="List"
                       Margin="Margin.Dense" Variant="Variant.Outlined">
                @if (ListsIn(_areaId).Count == 0)
                {
                    <MudSelectItem T="Guid" Value="@Guid.Empty">No lists in this area</MudSelectItem>
                }
                else
                {
                    @foreach (var list in ListsIn(_areaId))
                    {
                        <MudSelectItem T="Guid" Value="@list.Id">@list.Name</MudSelectItem>
                    }
                }
            </MudSelect>

            <MudButton Class="pspad-inbox-move" Variant="Variant.Filled" Color="Color.Primary"
                       Disabled="@(_listId == Guid.Empty)" OnClick="@MoveAsync">
                Move
            </MudButton>

            <MudButton Class="pspad-inbox-discard" Variant="Variant.Text" Color="Color.Error"
                       OnClick="@DiscardAsync">
                Discard
            </MudButton>
        </div>
    }
</MudDrawer>

@code {
    [Parameter] public InboxItem? Item { get; set; }

    [Parameter] public Guid InboxId { get; set; }

    [Parameter] public EventCallback OnClose { get; set; }

    [Parameter] public EventCallback OnChanged { get; set; }

    bool _isDesktop = true;
    IReadOnlyList<Area> _areas = [];
    IReadOnlyList<TaskList> _lists = [];
    Guid _areaId;
    Guid _listId;

    protected override async Task OnInitializedAsync() => await Viewport.SubscribeAsync(OnDesktopChanged);

    protected override async Task OnParametersSetAsync()
    {
        if (Item is null)
        {
            return;
        }

        var areas = await AreaStore.LoadAllAsync(State.UserId, CancellationToken.None);
        _areas = [.. areas.Where(area => !area.Deleted).OrderBy(area => area.Position)];

        var lists = await ListStore.LoadAllAsync(State.UserId, CancellationToken.None);
        _lists = [.. lists.Where(list => !list.Deleted).OrderBy(list => list.Position)];

        _areaId = _areas.Count > 0 ? _areas[0].Id : Guid.Empty;
        _listId = ListsIn(_areaId).FirstOrDefault()?.Id ?? Guid.Empty;
    }

    IReadOnlyList<TaskList> ListsIn(Guid areaId) => [.. _lists.Where(list => list.AreaId == areaId)];

    void OnAreaChanged(Guid areaId)
    {
        _areaId = areaId;
        _listId = ListsIn(areaId).FirstOrDefault()?.Id ?? Guid.Empty;
    }

    void OnDesktopChanged(bool isDesktop)
    {
        _isDesktop = isDesktop;
        InvokeAsync(StateHasChanged);
    }

    async Task OnOpenChanged(bool open)
    {
        if (!open)
        {
            await OnClose.InvokeAsync();
        }
    }

    async Task MoveAsync()
    {
        if (Item is null || _listId == Guid.Empty)
        {
            return;
        }

        await Sender.SendAsync(new OrganiseInboxItem(
            Guid.NewGuid(), State.UserId, InboxId, Item.Id, _listId, Guid.NewGuid()));

        await OnClose.InvokeAsync();
        await OnChanged.InvokeAsync();
    }

    async Task DiscardAsync()
    {
        if (Item is null)
        {
            return;
        }

        await Sender.SendAsync(new DiscardInboxItem(Guid.NewGuid(), State.UserId, InboxId, Item.Id));

        await OnClose.InvokeAsync();
        await OnChanged.InvokeAsync();
    }

    public async ValueTask DisposeAsync() => await Viewport.DisposeAsync();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~InboxItemPanelTests"`
Expected: PASS, all seven tests.

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.App/Components/InboxItemPanel.razor test/PSPad.App.Tests/Components/InboxItemPanelTests.cs
git commit -m "feat: add InboxItemPanel, a responsive move/discard drawer for inbox items"
```

---

### Task 4: `CaptureDialog` + rewire `InboxPage`

**Files:**
- Create: `src/PSPad.App/Components/CaptureDialog.razor`
- Modify: `src/PSPad.App/Pages/InboxPage.razor`
- Modify: `test/PSPad.App.Tests/Pages/InboxPageTests.cs`
- Modify: `src/PSPad.App/wwwroot/css/app.css` (drop the now-unused
  `.pspad-inbox-expanded` rule)

**Interfaces:**
- Consumes: `InboxItemCard` (Task 2, parameters `Item`, `OnOpen`),
  `InboxItemPanel` (Task 3, parameters `Item`, `InboxId`, `OnClose`,
  `OnChanged`), existing `PSPad.Module.Tasks.Inbox.CaptureToInbox(Guid
  commandId, Guid userId, Guid inboxId, Guid itemId, string text)`.
- Produces: `CaptureDialog` — an `IDialogService` dialog with no parameters,
  closes with `DialogResult.Ok(string text)` on Add or Enter, `Dialog.Cancel()`
  on Cancel. Its text input carries `placeholder="Capture"` (matches the
  selector the existing tests already use).

- [ ] **Step 1: Write the failing tests**

Replace the full contents of `test/PSPad.App.Tests/Pages/InboxPageTests.cs`:

```csharp
using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using PSPad.Abstractions;
using PSPad.App.Components;
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
    public async Task CapturingThroughTheFabDialogStoresTheItem()
    {
        var inbox = NewInbox();
        var replica = Arrange(inbox);

        var page = Render(BuildInboxPageWithDialogs());
        page.Find(".pspad-fab").Click();
        var dialog = page.FindComponent<MudDialogProvider>();
        dialog.Find("input[placeholder='Capture']").Input("Zadzwonić do serwisu");
        dialog.FindAll("button").Last().Click();

        var stored = await replica.LoadAsync<Inbox>(inbox.Id);
        Assert.Contains(stored!.Items, item => item.Text == "Zadzwonić do serwisu");
    }

    [Fact]
    public void ThereIsNoInlineCaptureFieldAnymoreOnlyTheFab()
    {
        Arrange(NewInbox());

        var page = Render<InboxPage>();

        Assert.Empty(page.FindAll("input[placeholder='Capture']"));
        page.Find(".pspad-fab");
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

    [Fact]
    public void EachItemIsItsOwnCard()
    {
        Arrange(NewInbox("Pierwsze", "Drugie"));

        var page = Render<InboxPage>();

        Assert.Equal(2, page.FindComponents<InboxItemCard>().Count);
    }

    [Fact]
    public void TappingAnItemOpensThePanelWithAreaAndListPickers()
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
    public async Task DiscardingFromThePanelRemovesTheItemAndCreatesNoTask()
    {
        var inbox = NewInbox("Nieaktualne");
        var replica = Arrange(inbox);

        var page = Render<InboxPage>();
        page.Find(".pspad-inbox-item").Click();
        page.Find("button.pspad-inbox-discard").Click();

        var stored = await replica.LoadAsync<Inbox>(inbox.Id);
        Assert.Empty(stored!.Items);
        Assert.Empty(await replica.LoadAllAsync<TodoTask>(User));
    }

    [Fact]
    public void ItLaysCapturedItemsOutInTheGrid()
    {
        Arrange(NewInbox("Pierwsze", "Drugie"));

        var page = Render<InboxPage>();

        page.Find(".pspad-grid");
    }

    [Fact]
    public void ItDoesNotClaimNothingIsCapturedBeforeItHasLoaded()
    {
        ArrangeWithPendingStore();

        var page = Render<InboxPage>();

        Assert.Single(page.FindComponents<RowSkeleton>());
        Assert.DoesNotContain("Nothing captured", page.Markup);
    }

    [Fact]
    public void WithNoListsTheMoveButtonIsDisabledRatherThanAbsent()
    {
        Arrange(NewInbox("Kupić mleko"), NewArea("Dom", 0));

        var page = Render<InboxPage>();
        page.Find(".pspad-inbox-item").Click();

        Assert.True(page.Find("button.pspad-inbox-move").HasAttribute("disabled"));
        Assert.Contains("No lists in this area", page.Markup);
    }

    // CaptureDialog and InboxItemCard both portal/render inside the page's own tree,
    // but MudDialogProvider must share a render tree with the page for the FAB's
    // dialog to be reachable by bUnit.
    RenderFragment BuildInboxPageWithDialogs() => builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<InboxPage>(2);
        builder.CloseComponent();
    };

    InMemoryReplica Arrange(params Aggregate[] documents) =>
        AppTestHost.Arrange(this, User, Today, documents);

    void ArrangeWithPendingStore()
    {
        Arrange();
        Services.AddSingleton<IDocumentStore<Inbox>>(new NeverLoadingInboxStore());
    }

    sealed class NeverLoadingInboxStore : IDocumentStore<Inbox>
    {
        public Task<Inbox?> LoadAsync(Guid id, CancellationToken ct) =>
            new TaskCompletionSource<Inbox?>().Task;

        public Task<IReadOnlyList<Inbox>> LoadAllAsync(Guid userId, CancellationToken ct) =>
            new TaskCompletionSource<IReadOnlyList<Inbox>>().Task;
    }

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

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~InboxPageTests"`
Expected: FAIL to compile — `CaptureDialog` doesn't exist yet, and
`InboxPage`'s current markup doesn't have a `.pspad-fab` or render
`InboxItemCard`/`InboxItemPanel`.

- [ ] **Step 3: Write `CaptureDialog`**

Create `src/PSPad.App/Components/CaptureDialog.razor`:

```razor
@using Microsoft.AspNetCore.Components.Web

<MudDialog>
    <DialogContent>
        <MudTextField T="string" @bind-Value="_text" Placeholder="Capture" AutoFocus="true"
                      Variant="Variant.Outlined" Immediate="true" OnKeyDown="@OnKeyDown" />
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => Dialog.Cancel())">Cancel</MudButton>
        <MudButton Color="Color.Primary" Disabled="@(_text.Length == 0)"
                   OnClick="@(() => Dialog.Close(DialogResult.Ok(_text)))">
            Add
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] IMudDialogInstance Dialog { get; set; } = null!;

    string _text = "";

    void OnKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Enter" && _text.Length > 0)
        {
            Dialog.Close(DialogResult.Ok(_text));
        }
    }
}
```

- [ ] **Step 4: Rewrite `InboxPage`**

Replace the full contents of `src/PSPad.App/Pages/InboxPage.razor`:

```razor
@page "/inbox"
@using PSPad.Abstractions
@using PSPad.App.Components
@using PSPad.App.State
@using PSPad.Module.Tasks.Inbox
@inject IDocumentStore<Inbox> InboxStore
@inject AppState State
@inject CommandSender Sender
@inject IDialogService Dialogs

<MudText Typo="Typo.h5" Color="Color.Primary" Class="mb-4">Inbox</MudText>

@if (!_loaded)
{
    <RowSkeleton Count="5" />
}
else
{
    <div class="pspad-grid">
        @foreach (var item in Items)
        {
            <InboxItemCard Item="@item" OnOpen="@Open" />
        }
    </div>

    @if (Items.Count == 0)
    {
        <MudText Typo="Typo.body2" Class="pa-4">Nothing captured.</MudText>
    }
}

<InboxItemPanel Item="@_openItem" InboxId="@(_inbox?.Id ?? Guid.Empty)" OnClose="@Close" OnChanged="@ReloadAsync" />

<MudFab Color="Color.Primary" StartIcon="@Icons.Material.Filled.Add" Class="pspad-fab" OnClick="@CaptureAsync" />

@code {
    bool _loaded;
    Inbox? _inbox;
    InboxItem? _openItem;

    IReadOnlyList<InboxItem> Items => _inbox?.Items ?? [];

    protected override async Task OnInitializedAsync() => await ReloadAsync();

    async Task ReloadAsync()
    {
        _inbox = (await InboxStore.LoadAllAsync(State.UserId, CancellationToken.None))
            .FirstOrDefault(inbox => !inbox.Deleted);

        _loaded = true;
    }

    void Open(InboxItem item) => _openItem = item;

    void Close() => _openItem = null;

    async Task CaptureAsync()
    {
        if (_inbox is null)
        {
            return;
        }

        var dialog = await Dialogs.ShowAsync<CaptureDialog>("Capture");
        var result = await dialog.Result;

        if (result is null || result.Canceled || result.Data is not string text || text.Length == 0)
        {
            return;
        }

        await Sender.SendAsync(new CaptureToInbox(
            Guid.NewGuid(), State.UserId, _inbox.Id, Guid.NewGuid(), text));

        await ReloadAsync();
    }
}
```

- [ ] **Step 5: Drop the now-unused CSS rule**

In `src/PSPad.App/wwwroot/css/app.css`, remove this rule (it targeted the
old inline-expanding row, which no longer exists):

```css
.pspad-inbox-expanded {
    grid-column: 1 / -1;
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~InboxPageTests"`
Expected: PASS, all tests.

- [ ] **Step 7: Run the full unit suite**

Run: `dotnet test --filter Category=Unit`
Expected: PASS, no regressions in `AreaBoardTests`, `TaskDetailPanelTests`,
`ListCardTests`, etc.

- [ ] **Step 8: Commit**

```bash
git add src/PSPad.App/Components/CaptureDialog.razor src/PSPad.App/Pages/InboxPage.razor \
  test/PSPad.App.Tests/Pages/InboxPageTests.cs src/PSPad.App/wwwroot/css/app.css
git commit -m "feat: redesign Inbox as cards with a FAB capture dialog and move/discard panel"
```
