using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Layout;
using PSPad.App.State;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class ListDetailPanelNavigationTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    readonly TaskList _list;

    public ListDetailPanelNavigationTests()
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), "Dom", 0),
            DateTimeOffset.UnixEpoch));
        _list = new TaskList();
        _list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), area.Id, "Zakupy"), DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, area, _list);
    }

    [Fact]
    public void DetailsShowsMembersAndSnapshotsRows()
    {
        var panel = Render(ListPanelView.Details);

        Assert.NotNull(panel.Find(".pspad-list-members-link"));
        Assert.NotNull(panel.Find(".pspad-list-snapshots-link"));
        Assert.Empty(panel.FindAll(".pspad-list-sharing"));
    }

    [Fact]
    public void TheMembersRowNavigatesToTheMembersView()
    {
        var panel = Render(ListPanelView.Details);

        panel.Find(".pspad-list-members-link").Click();

        Assert.EndsWith($"?list={_list.Id}&view=members", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void TheSnapshotsRowNavigatesToTheSnapshotsView()
    {
        var panel = Render(ListPanelView.Details);

        panel.Find(".pspad-list-snapshots-link").Click();

        Assert.EndsWith($"?list={_list.Id}&view=snapshots", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void TheMembersViewHasABackArrowAndNoDelete()
    {
        var panel = Render(ListPanelView.Members);

        Assert.Contains("Members", panel.Find(".pspad-panel-title").TextContent);
        Assert.Empty(panel.FindAll(".pspad-panel-delete"));
        panel.Find(".pspad-panel-back").Click();
        Assert.EndsWith($"?list={_list.Id}", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void TheSnapshotsViewIsTitledPublicSnapshots()
    {
        var panel = Render(ListPanelView.Snapshots);

        Assert.Contains("Public snapshots", panel.Find(".pspad-panel-title").TextContent);
        Assert.Empty(panel.FindAll(".pspad-list-name-field"));
    }

    IRenderedComponent<ContainerFragment> Render(ListPanelView view) => Render(builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<ListDetailPanel>(2);
        builder.AddAttribute(3, nameof(ListDetailPanel.ListId), (Guid?)_list.Id);
        builder.AddAttribute(4, nameof(ListDetailPanel.View), view);
        builder.CloseComponent();
    });
}
