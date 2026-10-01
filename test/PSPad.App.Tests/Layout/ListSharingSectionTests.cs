using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Layout;
using PSPad.App.State;
using PSPad.Module.Presentation.ListViews;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class ListSharingSectionTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly Guid Owner = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public async Task TurningTheLinkOnSharesTheList()
    {
        var area = NewArea(User, "Dom");
        var list = NewList(User, area.Id, "Zakupy");
        var replica = AppTestHost.Arrange(this, User, Today, area, list);
        Services.GetRequiredService<AppState>().DisplayName = "Lukasz";

        var panel = RenderWithOverlays(list.Id);
        panel.Find(".pspad-share-switch input").Change(true);

        var stored = await replica.LoadAsync<TaskList>(list.Id);
        Assert.NotNull(stored!.InviteToken);
        Assert.Equal(24, stored.InviteToken!.Length);
        Assert.Equal("Lukasz", stored.OwnerName);
        panel.WaitForAssertion(() => Assert.NotEmpty(panel.FindAll(".pspad-share-link")));
    }

    [Fact]
    public async Task TurningTheLinkOffStopsSharing()
    {
        var area = NewArea(User, "Dom");
        var list = Share(NewList(User, area.Id, "Zakupy"), "a".PadRight(24, 'x'), "Lukasz");
        var replica = AppTestHost.Arrange(this, User, Today, area, list);

        var panel = RenderWithOverlays(list.Id);
        panel.Find(".pspad-share-switch input").Change(false);

        var stored = await replica.LoadAsync<TaskList>(list.Id);
        Assert.Null(stored!.InviteToken);
        panel.WaitForAssertion(() => Assert.Empty(panel.FindAll(".pspad-share-link")));
    }

    [Fact]
    public async Task TheOwnerSeesMembersAndCanRemoveThem()
    {
        var area = NewArea(User, "Dom");
        var token = "shared-token-1234567890";
        var list = Join(Share(NewList(User, area.Id, "Zakupy"), token, "Lukasz"), token, Owner, "Kasia");
        var replica = AppTestHost.Arrange(this, User, Today, area, list);

        var panel = RenderWithOverlays(list.Id);
        Assert.Contains("Kasia", panel.Find(".pspad-share-member").TextContent);
        panel.Find(".pspad-share-remove").Click();
        panel.FindAll("div.mud-dialog button").Last().Click();

        var stored = await replica.LoadAsync<TaskList>(list.Id);
        Assert.DoesNotContain(stored!.Members, member => member.UserId == Owner);
    }

    [Fact]
    public void AMemberSeesWhoSharedItAndCanLeave()
    {
        var area = NewArea(Owner, "Dom");
        var list = Join(Share(NewList(Owner, area.Id, "Zakupy"), "shared-token-1234567890", "Kasia"), "shared-token-1234567890", User, "Lukasz");
        AppTestHost.Arrange(this, User, Today, list);

        var panel = RenderWithOverlays(list.Id);

        Assert.Contains("Shared by Kasia", panel.Find(".pspad-share-owner").TextContent);
        panel.Find(".pspad-share-leave").Click();
        panel.FindAll("div.mud-dialog button").Last().Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith($"/areas/{SharedWithMe.AreaId}", nav.Uri);
    }

    [Fact]
    public void AMemberCannotRenameOrDelete()
    {
        var area = NewArea(Owner, "Dom");
        var list = Join(Share(NewList(Owner, area.Id, "Zakupy"), "shared-token-1234567890", "Kasia"), "shared-token-1234567890", User, "Lukasz");
        AppTestHost.Arrange(this, User, Today, list);

        var panel = RenderWithOverlays(list.Id);

        Assert.True(panel.Find(".pspad-list-name-field input").HasAttribute("disabled"));
        Assert.Empty(panel.FindAll(".pspad-panel-delete"));
    }

    [Fact]
    public async Task AMemberFilesTheListUnderTheirArea()
    {
        var ownerArea = NewArea(Owner, "Dom");
        var myArea = NewArea(User, "Praca");
        var list = Join(Share(NewList(Owner, ownerArea.Id, "Zakupy"), "shared-token-1234567890", "Kasia"), "shared-token-1234567890", User, "Lukasz");
        var replica = AppTestHost.Arrange(this, User, Today, myArea, list);

        var panel = RenderWithOverlays(list.Id);
        panel.Find(".pspad-list-filed-area .mud-select-input").MouseDown();
        panel.WaitForAssertion(() => Assert.Contains(panel.FindAll(".mud-list-item"),
            option => option.TextContent.Trim() == "Praca"));
        panel.FindAll(".mud-list-item").First(option => option.TextContent.Trim() == "Praca").Click();

        var views = await replica.LoadAllAsync<ListView>(User);
        var view = Assert.Single(views);
        Assert.Equal(myArea.Id, view.AreaId);
    }

    IRenderedComponent<ContainerFragment> RenderWithOverlays(Guid listId) => Render(builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<ListDetailPanel>(2);
        builder.AddAttribute(3, nameof(ListDetailPanel.ListId), (Guid?)listId);
        builder.CloseComponent();
    });

    static Area NewArea(Guid userId, string name)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(null, new CreateArea(Guid.NewGuid(), userId, Guid.NewGuid(), name, 0),
            DateTimeOffset.UnixEpoch));
        return area;
    }

    static TaskList NewList(Guid userId, Guid areaId, string name)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), userId, Guid.NewGuid(), areaId, name), DateTimeOffset.UnixEpoch));
        return list;
    }

    static TaskList Share(TaskList list, string token, string ownerName)
    {
        list.ApplyAll(TaskList.Decide(
            list, new ShareTaskList(Guid.NewGuid(), list.UserId, list.Id, token, ownerName), DateTimeOffset.UnixEpoch));
        return list;
    }

    static TaskList Join(TaskList list, string token, Guid memberId, string displayName)
    {
        list.ApplyAll(TaskList.Decide(
            list, new JoinTaskList(Guid.NewGuid(), memberId, list.Id, token, displayName), DateTimeOffset.UnixEpoch));
        return list;
    }
}
