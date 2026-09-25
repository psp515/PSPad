using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.Abstractions;
using PSPad.App.Components;
using PSPad.App.Pages;
using PSPad.App.State.Outbox;
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
    public void ItLaysCapturedItemsOutInTheGrid()
    {
        Arrange(NewInbox("Pierwsze", "Drugie"));

        var page = Render<InboxPage>();

        page.Find(".mud-grid");
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
    public void TheFabOpensTheCapturePanel()
    {
        Arrange(NewInbox());
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/inbox");

        var page = Render<InboxPage>();
        var fab = page.Find(".pspad-fab");
        Assert.Equal("Capture", fab.GetAttribute("aria-label"));
        fab.Click();

        Assert.EndsWith("/inbox?inbox=new", navigation.Uri);
    }

    [Fact]
    public void AnEmptyInboxShowsTheEmptyStateThatOpensTheCapturePanel()
    {
        Arrange(NewInbox());
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/inbox");

        var page = Render<InboxPage>();
        var empty = page.FindComponent<EmptyState>();
        Assert.Contains("Nothing captured.", empty.Markup);
        empty.Find(".pspad-empty-state").Click();

        Assert.EndsWith("/inbox?inbox=new", navigation.Uri);
    }

    [Fact]
    public void AnInboxWithItemsHasNoEmptyState()
    {
        Arrange(NewInbox("Kupić mleko"));

        var page = Render<InboxPage>();

        Assert.Empty(page.FindComponents<EmptyState>());
    }

    [Fact]
    public void TappingAnItemOpensItsPanel()
    {
        var inbox = NewInbox("Kupić mleko");
        Arrange(inbox);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/inbox");

        var page = Render<InboxPage>();
        page.Find(".pspad-inbox-item").Click();

        Assert.EndsWith($"/inbox?inbox={inbox.Items[0].Id}", navigation.Uri);
    }

    [Fact]
    public async Task AnItemCapturedElsewhereShowsUpWithoutAReload()
    {
        var inbox = NewInbox();
        Arrange(inbox);

        var page = Render<InboxPage>();
        var sender = Services.GetRequiredService<PSPad.App.State.Dispatch.CommandSender>();
        await page.InvokeAsync(() => sender.SendAsync(
            new CaptureToInbox(Guid.NewGuid(), User, inbox.Id, Guid.NewGuid(), "Kupić farbę")));

        page.WaitForAssertion(() => Assert.Contains("Kupić farbę", page.Markup));
    }

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
