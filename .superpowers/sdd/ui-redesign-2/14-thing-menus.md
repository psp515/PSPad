# Plan 14 — `⋯` menus, and the New area button that does nothing

**Goal.** Rename, delete and move live in a `⋯` menu on the thing itself, for
areas and for lists. And `+ New area` starts creating an area — today the button
raises a callback that only closes the drawer, so **areas cannot be created from
the UI at all**. `grep -r CreateArea src/PSPad.App` returns nothing.

**Spec.** `specs/ui-redesign-2-design.md` D7 (`⋯` menu on the thing, no
management screen), D6 (creation offered where the thing is created — the area
row of that table is unbuilt).

**Architecture.** Presentation only. Every action is an existing command:
`RenameArea`, `DeleteArea`, `CreateArea`, `RenameTaskList`, `DeleteTaskList`,
`MoveTaskListToArea`. Areas live on `AppShell` (it owns `_areas` and passes them
to `NavSidebar`), so area actions are callbacks up to the shell; list actions
belong to the screen that renders the list.

**Constraints.** This track owns `Components/ThingMenu.razor` (new),
`Components/ConfirmDialog.razor` (new), `Layout/NavSidebar.razor`,
`Layout/AppShell.razor`, `Components/ListCard.razor`, `Pages/AreaBoard.razor`,
`Pages/ListPage.razor`, `wwwroot/css/app.css`, and the matching tests. It must
not touch `Pages/Today.razor` or `Pages/InboxPage.razor` — plans 12 and 13 hold
those. `TaskRow` read-only.

**Interfaces consumed**

```csharp
RenameArea(Guid CommandId, Guid UserId, Guid AreaId, string Name)
DeleteArea(Guid CommandId, Guid UserId, Guid AreaId)
CreateArea(Guid CommandId, Guid UserId, Guid AreaId, string Name, int Position)
RenameTaskList(Guid CommandId, Guid UserId, Guid ListId, string Name)
DeleteTaskList(Guid CommandId, Guid UserId, Guid ListId)
MoveTaskListToArea(Guid CommandId, Guid UserId, Guid ListId, Guid AreaId)
Positions.Next(IEnumerable<int> taken) -> int
NameDialog: parameters Title, Value; closes with DialogResult.Ok(string)
```

**Interfaces produced**

```csharp
// Components/ThingMenu.razor
[Parameter] EventCallback OnRename
[Parameter] EventCallback OnDelete
[Parameter] RenderFragment? Extra          // extra MudMenuItems, e.g. "Move to area"
[Parameter] string Class

// Components/ConfirmDialog.razor
[Parameter] string Message
// closes with DialogResult.Ok(true) or Cancel
```

---

## Task 1 — `ThingMenu` and `ConfirmDialog`

**Files**
- create `src/PSPad.App/Components/ThingMenu.razor`
- create `src/PSPad.App/Components/ConfirmDialog.razor`
- create `test/PSPad.App.Tests/Components/ThingMenuTests.cs`

- [ ] Write the test first:

```csharp
using Bunit;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class ThingMenuTests : Bunit.TestContext
{
    [Fact]
    public void ItOffersRenameAndDeleteAndRaisesThem()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var renamed = false;
        var deleted = false;

        var menu = Render<ThingMenu>(parameters => parameters
            .Add(p => p.OnRename, () => renamed = true)
            .Add(p => p.OnDelete, () => deleted = true));

        menu.Find("button").Click();

        var items = menu.FindAll(".mud-list-item");
        Assert.Equal(2, items.Count);

        items[0].Click();
        Assert.True(renamed);

        menu.Find("button").Click();
        menu.FindAll(".mud-list-item")[1].Click();
        Assert.True(deleted);
    }
}
```

- [ ] `ThingMenu.razor`:

```razor
<MudMenu Icon="@Icons.Material.Filled.MoreHoriz" Size="Size.Small" Dense="true"
         AnchorOrigin="Origin.BottomRight" TransformOrigin="Origin.TopRight" Class="@Class">
    <MudMenuItem OnClick="@OnRename">Rename</MudMenuItem>
    @Extra
    <MudMenuItem OnClick="@OnDelete">Delete</MudMenuItem>
</MudMenu>

@code {
    [Parameter] public EventCallback OnRename { get; set; }

    [Parameter] public EventCallback OnDelete { get; set; }

    [Parameter] public RenderFragment? Extra { get; set; }

    [Parameter] public string Class { get; set; } = "";
}
```

- [ ] `ConfirmDialog.razor`:

```razor
<MudDialog>
    <DialogContent>
        <MudText Typo="Typo.body1">@Message</MudText>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => Dialog.Cancel())">Cancel</MudButton>
        <MudButton Color="Color.Error" OnClick="@(() => Dialog.Close(DialogResult.Ok(true)))">
            Delete
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] IMudDialogInstance Dialog { get; set; } = null!;

    [Parameter] public string Message { get; set; } = "Delete this?";
}
```

- [ ] Run:

```bash
dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"
```

- [ ] Commit: `feat(app): a shared thing menu and a confirm dialog`

---

## Task 2 — `+ New area` creates an area

The dead button. `NavSidebar.OnNewArea` is raised on click; the persistent
drawer passes no handler at all and the temporary one passes
`() => _drawerOpen = false`. Give the shell a real handler and keep the drawer
close.

**Files**
- modify `test/PSPad.App.Tests/Layout/AppShellTests.cs`
- modify `src/PSPad.App/Layout/AppShell.razor`

- [ ] Add to `AppShellTests`:

```csharp
    [Fact]
    public void TheNewAreaButtonIsWiredToAHandler()
    {
        AppTestHost.Arrange(this, User, Today);

        var shell = Render<AppShell>();
        var sidebars = shell.FindComponents<NavSidebar>();

        Assert.All(sidebars, sidebar =>
            Assert.True(sidebar.Instance.OnNewArea.HasDelegate));
    }
```

Add `using PSPad.App.Layout;` if it is not already there.

- [ ] In `AppShell.razor`, give both drawers the same handler:

```razor
        <NavSidebar Areas="@_areas" Email="@_email" DisplayName="@_displayName" UserId="@State.UserId"
                    PendingCommands="@Coordinator.PendingCount"
                    OnNewArea="@NewAreaAsync"
                    OnRenameArea="@RenameAreaAsync"
                    OnDeleteArea="@DeleteAreaAsync" />
```

and for the temporary drawer the same three plus `Navigated="@CloseDrawer"`.

- [ ] Add to `AppShell`'s `@code`:

```csharp
    [Inject] IDialogService Dialogs { get; set; } = null!;

    void CloseDrawer() => _drawerOpen = false;

    async Task NewAreaAsync()
    {
        _drawerOpen = false;

        var name = await AskForNameAsync("New area", "Area name", "");
        if (name is null)
        {
            return;
        }

        await Sender.SendAsync(new CreateArea(
            Guid.NewGuid(), State.UserId, Guid.NewGuid(), name,
            Positions.Next(_areas.Select(area => area.Position))));

        await ReloadAreasAndCountsAsync();
    }

    async Task RenameAreaAsync(Area area)
    {
        var name = await AskForNameAsync("Rename area", "Area name", area.Name);
        if (name is null)
        {
            return;
        }

        await Sender.SendAsync(new RenameArea(Guid.NewGuid(), State.UserId, area.Id, name));
        await ReloadAreasAndCountsAsync();
    }

    async Task DeleteAreaAsync(Area area)
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { dialog => dialog.Message, $"Delete “{area.Name}”? Its lists stay, without an area." }
        };
        var dialog = await Dialogs.ShowAsync<ConfirmDialog>("Delete area", parameters);
        var result = await dialog.Result;

        if (result is null || result.Canceled)
        {
            return;
        }

        await Sender.SendAsync(new DeleteArea(Guid.NewGuid(), State.UserId, area.Id));
        await ReloadAreasAndCountsAsync();

        if (Navigation.Uri.Contains($"/areas/{area.Id}", StringComparison.OrdinalIgnoreCase))
        {
            Navigation.NavigateTo("/");
        }
    }

    async Task<string?> AskForNameAsync(string title, string label, string value)
    {
        var parameters = new DialogParameters<NameDialog>
        {
            { dialog => dialog.Title, label },
            { dialog => dialog.Value, value }
        };
        var dialog = await Dialogs.ShowAsync<NameDialog>(title, parameters);
        var result = await dialog.Result;

        return result is null || result.Canceled || result.Data is not string name || name.Length == 0
            ? null
            : name;
    }
```

Add `@using PSPad.App.Components` and `@using PSPad.Module.Tasks.Ordering` to
the top of `AppShell.razor`.

**Note on `DeleteArea`.** Deletion does not cascade in this domain (the search
work established it: lists of a deleted area stay reachable). The confirm text
above says so rather than implying a cascade that does not happen.

- [ ] Run:

```bash
dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"
```

- [ ] Commit: `fix(app): the new area button creates an area`

---

## Task 3 — The area row carries its menu

**Files**
- modify `test/PSPad.App.Tests/Layout/NavSidebarTests.cs`
- modify `src/PSPad.App/Layout/NavSidebar.razor`
- modify `src/PSPad.App/wwwroot/css/app.css`

- [ ] Add to `NavSidebarTests`:

```csharp
    [Fact]
    public void EveryAreaRowCarriesAMenu()
    {
        AppTestHost.Arrange(this, User, Today);

        var sidebar = Render<NavSidebar>(parameters => parameters
            .Add(p => p.Areas, new[] { NewArea("Dom", 0), NewArea("Praca", 1) }));

        Assert.Equal(2, sidebar.FindComponents<ThingMenu>().Count);
    }

    [Fact]
    public void RenamingAnAreaRaisesItWithTheArea()
    {
        AppTestHost.Arrange(this, User, Today);
        var area = NewArea("Dom", 0);
        Area? renamed = null;

        var sidebar = Render<NavSidebar>(parameters => parameters
            .Add(p => p.Areas, new[] { area })
            .Add(p => p.OnRenameArea, a => renamed = a));

        sidebar.Find(".pspad-area-menu button").Click();
        sidebar.FindAll(".mud-list-item")[0].Click();

        Assert.Equal(area.Id, renamed?.Id);
    }
```

Add `using PSPad.App.Components;` and a `NewArea` helper matching plan 13's.

- [ ] In `NavSidebar.razor`, replace the area `MudNavLink` loop with a row that
      keeps the link and adds the menu:

```razor
                @foreach (var area in Areas.OrderBy(area => area.Position))
                {
                    <div class="d-flex align-center pspad-area-row">
                        <div class="flex-grow-1">
                            <MudNavLink Href="@($"/areas/{area.Id}")" Icon="@Icons.Material.Filled.Folder">
                                @area.Name
                            </MudNavLink>
                        </div>
                        <ThingMenu Class="pspad-area-menu mr-2"
                                   OnRename="@(() => OnRenameArea.InvokeAsync(area))"
                                   OnDelete="@(() => OnDeleteArea.InvokeAsync(area))" />
                    </div>
                }
```

- [ ] Add the parameters:

```csharp
    [Parameter] public EventCallback<Area> OnRenameArea { get; set; }

    [Parameter] public EventCallback<Area> OnDeleteArea { get; set; }
```

- [ ] Add `@using PSPad.App.Components` to the top of `NavSidebar.razor`.

- [ ] In `app.css`, keep the menu from stretching the nav row:

```css
.pspad-area-row .mud-nav-link {
    padding-right: 0;
}
```

- [ ] Run:

```bash
dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"
```

- [ ] Commit: `feat(app): rename and delete an area from its sidebar row`

---

## Task 4 — The list card header carries its menu

**Files**
- modify `test/PSPad.App.Tests/Components/ListCardTests.cs`
- modify `src/PSPad.App/Components/ListCard.razor`

- [ ] Add to `ListCardTests`:

```csharp
    [Fact]
    public void TheHeaderCarriesAMenuThatRaisesRenameAndDelete()
    {
        var list = NewList("Zakupy");
        var renamed = false;
        var deleted = false;

        var card = Render<ListCard>(parameters => parameters
            .Add(p => p.List, list)
            .Add(p => p.OnRename, () => renamed = true)
            .Add(p => p.OnDelete, () => deleted = true));

        card.Find(".pspad-list-menu button").Click();
        card.FindAll(".mud-list-item")[0].Click();
        Assert.True(renamed);

        card.Find(".pspad-list-menu button").Click();
        card.FindAll(".mud-list-item").Last().Click();
        Assert.True(deleted);
    }
```

- [ ] In `ListCard.razor`, after the open count in the header:

```razor
        <ThingMenu Class="pspad-list-menu"
                   OnRename="@OnRename" OnDelete="@OnDelete">
            <Extra>
                <MudMenuItem OnClick="@OnMove">Move to area</MudMenuItem>
            </Extra>
        </ThingMenu>
```

- [ ] Add the parameters:

```csharp
    [Parameter] public EventCallback OnRename { get; set; }

    [Parameter] public EventCallback OnDelete { get; set; }

    [Parameter] public EventCallback OnMove { get; set; }
```

- [ ] Run:

```bash
dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"
```

- [ ] Commit: `feat(app): a menu on the list card header`

---

## Task 5 — The area screen answers the card's menu

**Files**
- modify `test/PSPad.App.Tests/Pages/AreaBoardTests.cs`
- modify `src/PSPad.App/Pages/AreaBoard.razor`

- [ ] Add to `AreaBoardTests`:

```csharp
    [Fact]
    public async Task DeletingAListFromItsCardRemovesItFromTheScreen()
    {
        var area = NewArea("Dom", 0);
        var list = NewList(area.Id, "Zakupy");
        var replica = Arrange(area, list);

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, area.Id));
        page.Find(".pspad-list-menu button").Click();
        page.FindAll(".mud-list-item").Last().Click();

        var dialog = page.FindComponent<MudDialogProvider>();
        dialog.FindAll("button").Last().Click();

        var stored = await replica.LoadAsync<TaskList>(list.Id);
        Assert.True(stored!.Deleted);
    }
```

The test renders inside `AppTestHost`; add a `MudDialogProvider` to the render
tree the way `AreaBoardTests` already does for `NameDialog` (if it does not yet,
wrap with `RenderWithProviders` — copy the arrangement from
`TaskDetailPanelTests`, which already shows dialogs).

- [ ] In `AreaBoard.razor`, pass the three callbacks to each card:

```razor
        <ListCard List="@list" Tasks="@TasksOf(list.Id)" Today="@State.Today"
                  OnToggle="@ToggleAsync" OnStar="@StarAsync" OnOpen="@Open"
                  OnAddTask="@(name => AddTaskAsync(list.Id, name))"
                  OnRename="@(() => RenameListAsync(list))"
                  OnDelete="@(() => DeleteListAsync(list))"
                  OnMove="@(() => MoveListAsync(list))" />
```

- [ ] Add the handlers:

```csharp
    async Task RenameListAsync(TaskList list)
    {
        var parameters = new DialogParameters<NameDialog>
        {
            { dialog => dialog.Title, "List name" },
            { dialog => dialog.Value, list.Name }
        };
        var dialog = await Dialogs.ShowAsync<NameDialog>("Rename list", parameters);
        var result = await dialog.Result;

        if (result is null || result.Canceled || result.Data is not string name || name.Length == 0)
        {
            return;
        }

        await Sender.SendAsync(new RenameTaskList(Guid.NewGuid(), State.UserId, list.Id, name));
        await ReloadAsync();
    }

    async Task DeleteListAsync(TaskList list)
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { dialog => dialog.Message, $"Delete “{list.Name}”? Its tasks stay, without a list." }
        };
        var dialog = await Dialogs.ShowAsync<ConfirmDialog>("Delete list", parameters);
        var result = await dialog.Result;

        if (result is null || result.Canceled)
        {
            return;
        }

        await Sender.SendAsync(new DeleteTaskList(Guid.NewGuid(), State.UserId, list.Id));
        await ReloadAsync();
    }

    async Task MoveListAsync(TaskList list)
    {
        var areas = await AreaStore.LoadAllAsync(State.UserId, CancellationToken.None);
        var targets = areas.Where(area => !area.Deleted && area.Id != AreaId).ToArray();

        if (targets.Length == 0)
        {
            return;
        }

        var parameters = new DialogParameters<AreaPickerDialog>
        {
            { dialog => dialog.Areas, targets }
        };
        var dialog = await Dialogs.ShowAsync<AreaPickerDialog>("Move to area", parameters);
        var result = await dialog.Result;

        if (result is null || result.Canceled || result.Data is not Guid target)
        {
            return;
        }

        await Sender.SendAsync(new MoveTaskListToArea(Guid.NewGuid(), State.UserId, list.Id, target));
        await ReloadAsync();
    }
```

- [ ] Create `src/PSPad.App/Components/AreaPickerDialog.razor`:

```razor
@using PSPad.Module.Tasks.Areas

<MudDialog>
    <DialogContent>
        <MudSelect T="Guid" @bind-Value="_areaId" Label="Area" Variant="Variant.Outlined"
                   Margin="Margin.Dense">
            @foreach (var area in Areas)
            {
                <MudSelectItem T="Guid" Value="@area.Id">@area.Name</MudSelectItem>
            }
        </MudSelect>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => Dialog.Cancel())">Cancel</MudButton>
        <MudButton Color="Color.Primary" Disabled="@(_areaId == Guid.Empty)"
                   OnClick="@(() => Dialog.Close(DialogResult.Ok(_areaId)))">
            Move
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] IMudDialogInstance Dialog { get; set; } = null!;

    [Parameter] public IReadOnlyList<Area> Areas { get; set; } = [];

    Guid _areaId;

    protected override void OnInitialized() => _areaId = Areas.Count > 0 ? Areas[0].Id : Guid.Empty;
}
```

- [ ] Run:

```bash
dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"
```

- [ ] Commit: `feat(app): rename, delete and move a list from its card`

---

## Task 6 — The list screen's title carries the same menu

**Files**
- modify `test/PSPad.App.Tests/Pages/ListPageTests.cs`
- modify `src/PSPad.App/Pages/ListPage.razor`

- [ ] Add to `ListPageTests`:

```csharp
    [Fact]
    public async Task RenamingFromTheTitleMenuRenamesTheList()
    {
        var list = NewList("Zakupy");
        var replica = Arrange(list);

        var page = Render<ListPage>(parameters => parameters.Add(p => p.ListId, list.Id));
        page.Find(".pspad-list-menu button").Click();
        page.FindAll(".mud-list-item")[0].Click();

        var field = page.Find("div.mud-dialog input");
        field.Input("Zakupy tygodniowe");
        page.FindAll("div.mud-dialog button").Last().Click();

        var stored = await replica.LoadAsync<TaskList>(list.Id);
        Assert.Equal("Zakupy tygodniowe", stored!.Name);
    }
```

- [ ] In `ListPage.razor`, put the title and menu on one row:

```razor
    <div class="d-flex align-center mb-4">
        <MudText Typo="Typo.h5" Color="Color.Primary">@_list.Name</MudText>
        <ThingMenu Class="pspad-list-menu ml-2"
                   OnRename="@RenameAsync" OnDelete="@DeleteAsync" />
    </div>
```

- [ ] Add handlers mirroring task 5's, with `Dialogs` injected; after a
      successful delete, `Navigation.NavigateTo($"/areas/{_list.AreaId}")`.

- [ ] Run:

```bash
dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"
```

- [ ] Commit: `feat(app): rename and delete a list from its screen`

---

## Done when

- An area can be created, renamed and deleted from the sidebar.
- A list can be renamed, deleted and moved to another area from its card and
  renamed or deleted from its screen.
- Deleting the open area or list leaves the user somewhere that exists.
- `dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"` green.

## Not in this plan

Reordering. D7 says the `⋯` menu moves items and the design's out-of-scope
section rules out drag-and-drop, but no `MoveArea` command exists — `Positions`
has `Move`, and nothing calls it for areas. Adding one is a module change and
this is a presentation track. Raise it as its own plan.
