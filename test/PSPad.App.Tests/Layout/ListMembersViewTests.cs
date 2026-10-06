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
public class ListMembersViewTests : Bunit.TestContext
{
    const string Token = "shared-token-1234567890";
    const string Code = "K7M4PX";
    static readonly Guid User = Guid.NewGuid();
    static readonly Guid Owner = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);
    static readonly DateTimeOffset Midnight = new(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    [Fact]
    public async Task TheOwnerSeesMembersAndCanRemoveThem()
    {
        var area = NewArea(User, "Dom");
        var list = Join(Share(NewList(User, area.Id), Midnight), Token, Owner, "Kasia");
        var replica = AppTestHost.Arrange(this, User, Today, area, list);

        var panel = RenderWithOverlays(list.Id, ListPanelView.Members);
        Assert.Contains("Kasia", panel.Find(".pspad-share-member").TextContent);
        panel.Find(".pspad-share-remove").Click();
        panel.FindAll("div.mud-dialog button").Last().Click();

        var stored = await replica.LoadAsync<TaskList>(list.Id);
        Assert.DoesNotContain(stored!.Members, member => member.UserId == Owner);
    }

    [Fact]
    public void AMemberSeesWhoSharedItAndCanLeave()
    {
        var list = Join(Share(NewList(Owner), Midnight), Token, User, "Lukasz");
        AppTestHost.Arrange(this, User, Today, list);

        var panel = RenderWithOverlays(list.Id, ListPanelView.Members);

        Assert.Contains("Kasia", panel.Find(".pspad-share-owner").TextContent);
        panel.Find(".pspad-share-leave").Click();
        panel.FindAll("div.mud-dialog button").Last().Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith(SharedWithMe.Href, nav.Uri);
    }

    [Fact]
    public void AMemberCannotRenameOrDelete()
    {
        var list = Join(Share(NewList(Owner), Midnight), Token, User, "Lukasz");
        AppTestHost.Arrange(this, User, Today, list);

        var panel = RenderWithOverlays(list.Id, ListPanelView.Details);

        Assert.True(panel.Find(".pspad-list-name-field input").HasAttribute("disabled"));
        Assert.Empty(panel.FindAll(".pspad-panel-delete"));
        Assert.Empty(panel.FindAll(".pspad-list-filed-area"));
    }

    [Fact]
    public async Task AMemberFilesTheListUnderTheirArea()
    {
        var myArea = NewArea(User, "Praca");
        var list = Join(Share(NewList(Owner), Midnight), Token, User, "Lukasz");
        var replica = AppTestHost.Arrange(this, User, Today, myArea, list);

        var panel = RenderWithOverlays(list.Id, ListPanelView.Members);
        panel.Find(".pspad-list-filed-area .mud-select-input").MouseDown();
        panel.WaitForAssertion(() => Assert.Contains(panel.FindAll(".mud-list-item"),
            option => option.TextContent.Trim() == "Praca"));
        panel.FindAll(".mud-list-item").First(option => option.TextContent.Trim() == "Praca").Click();

        var views = await replica.LoadAllAsync<ListView>(User);
        var view = Assert.Single(views);
        Assert.Equal(myArea.Id, view.AreaId);
    }

    [Fact]
    public void AMemberNeverSeesTheInviteLink()
    {
        var list = Join(Share(NewList(Owner), Midnight), Token, User, "Lukasz");
        AppTestHost.Arrange(this, User, Today, list);

        var panel = RenderWithOverlays(list.Id, ListPanelView.Members);

        Assert.Empty(panel.FindAll(".pspad-invite-copy"));
        Assert.Empty(panel.FindAll("img.pspad-qr"));
        Assert.Contains("Only Kasia can invite people.", panel.Markup);
    }

    [Fact]
    public async Task StoppingSharingRemovesEveryone()
    {
        var list = Join(Share(NewList(User), Midnight), Token, Owner, "Kasia");
        var replica = AppTestHost.Arrange(this, User, Today, list);

        var panel = RenderWithOverlays(list.Id, ListPanelView.Members);
        panel.Find(".pspad-share-stop").Click();
        panel.FindAll("div.mud-dialog button").Last().Click();

        var stored = await replica.LoadAsync<TaskList>(list.Id);
        Assert.Null(stored!.InviteToken);
        Assert.Empty(stored.Members);
    }

    IRenderedComponent<ContainerFragment> RenderWithOverlays(Guid listId, ListPanelView view) => Render(builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<ListDetailPanel>(2);
        builder.AddAttribute(3, nameof(ListDetailPanel.ListId), (Guid?)listId);
        builder.AddAttribute(4, nameof(ListDetailPanel.View), view);
        builder.CloseComponent();
    });

    static Area NewArea(Guid userId, string name)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(null, new CreateArea(Guid.NewGuid(), userId, Guid.NewGuid(), name, 0),
            DateTimeOffset.UnixEpoch));
        return area;
    }

    static TaskList NewList(Guid userId) => NewList(userId, Guid.NewGuid());

    static TaskList NewList(Guid userId, Guid areaId)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), userId, Guid.NewGuid(), areaId, "Zakupy"), DateTimeOffset.UnixEpoch));
        return list;
    }

    static TaskList Share(TaskList list, DateTimeOffset at)
    {
        var ownerName = list.UserId == Owner ? "Kasia" : "Lukasz";
        list.ApplyAll(TaskList.Decide(
            list, new ShareTaskList(Guid.NewGuid(), list.UserId, list.Id, Token, Code, ownerName), at));
        return list;
    }

    static TaskList Join(TaskList list, string token, Guid memberId, string displayName)
    {
        var at = list.InviteExpiresAt!.Value - TaskList.InviteLife;
        list.ApplyAll(TaskList.Decide(
            list, new JoinTaskList(Guid.NewGuid(), memberId, list.Id, token, Code, displayName), at));
        return list;
    }
}
