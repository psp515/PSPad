using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.Abstractions;
using PSPad.App.Components;
using PSPad.App.Pages;
using PSPad.App.State.Replica;
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
