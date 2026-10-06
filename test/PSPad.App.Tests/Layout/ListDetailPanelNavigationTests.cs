using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Api;
using PSPad.App.Layout;
using PSPad.Contracts;
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

    [Fact]
    public void AFailingSnapshotsApiStillRendersDetailsOffline()
    {
        Services.AddSingleton<ISnapshotsApi>(new CountingSnapshotsApi(throws: true));

        var panel = Render(ListPanelView.Details);

        panel.WaitForAssertion(() =>
            Assert.Contains("Needs a connection", panel.Find(".pspad-list-snapshots-link").TextContent));
        Assert.NotNull(panel.Find(".pspad-list-members-link"));
    }

    [Fact]
    public void ReRenderingTheSameListLoadsTheSummaryOnce()
    {
        var api = new CountingSnapshotsApi(throws: false);
        Services.AddSingleton<ISnapshotsApi>(api);

        var panel = Render<ListDetailPanel>(parameters => parameters
            .Add(p => p.ListId, _list.Id)
            .Add(p => p.View, ListPanelView.Details)
            .Add(p => p.OnClose, EventCallback.Factory.Create(this, () => { })));
        panel.WaitForAssertion(() =>
            Assert.Contains("None live", panel.Find(".pspad-list-snapshots-link").TextContent));

        for (var round = 0; round < 2; round++)
        {
            panel.Render(parameters => parameters
                .Add(p => p.OnClose, EventCallback.Factory.Create(this, () => { })));
        }

        Assert.Equal(1, api.Calls);
    }

    [Fact]
    public void ANonOwnerAtSnapshotsGetsTheDetailsViewWithoutBack()
    {
        var owner = Guid.NewGuid();
        var foreign = new TaskList();
        foreign.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), owner, Guid.NewGuid(), Guid.NewGuid(), "Cudza"), DateTimeOffset.UnixEpoch));
        foreign.ApplyAll(TaskList.Decide(
            foreign, new ShareTaskList(Guid.NewGuid(), owner, foreign.Id, "shared-token-1234567890", "K7M4PX", "Kasia"), DateTimeOffset.UnixEpoch));
        foreign.ApplyAll(TaskList.Decide(
            foreign, new JoinTaskList(Guid.NewGuid(), User, foreign.Id, "shared-token-1234567890", "K7M4PX", "Ja"), DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, foreign);

        var panel = Render(foreign.Id, ListPanelView.Snapshots);

        Assert.DoesNotContain("Public snapshots", panel.Find(".pspad-panel-title").TextContent);
        Assert.Empty(panel.FindAll(".pspad-panel-back"));
        Assert.NotNull(panel.Find(".pspad-list-members-link"));
    }

    sealed class CountingSnapshotsApi(bool throws) : ISnapshotsApi
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<PublishedSnapshotView>> ForListAsync(Guid listId)
        {
            Calls++;
            return throws
                ? throw new HttpRequestException("down")
                : Task.FromResult<IReadOnlyList<PublishedSnapshotView>>([]);
        }

        public Task<PublishedSnapshotView?> PublishAsync(Guid listId, DateTimeOffset expiresAt) =>
            Task.FromResult<PublishedSnapshotView?>(null);

        public Task<bool> RevokeAsync(Guid snapshotId) => Task.FromResult(false);

        public Task<bool> RecordVisitAsync(string token) => Task.FromResult(false);

        public Task<IReadOnlyList<SnapshotVisitView>> VisitsAsync() =>
            Task.FromResult<IReadOnlyList<SnapshotVisitView>>([]);
    }

    IRenderedComponent<ContainerFragment> Render(ListPanelView view) => Render(_list.Id, view);

    IRenderedComponent<ContainerFragment> Render(Guid listId, ListPanelView view) => Render(builder =>
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
}
