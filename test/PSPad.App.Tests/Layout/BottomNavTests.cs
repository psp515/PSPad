using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Layout;
using PSPad.App.State;
using PSPad.App.State.Replica;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class BottomNavTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void ItIsPhoneOnlyAndSticksToTheBottom()
    {
        Arrange();

        var nav = Render<BottomNav>();

        var bar = nav.Find(".mud-appbar");
        Assert.Contains("d-md-none", bar.ClassList);
        Assert.Contains("mud-appbar-fixed-bottom", bar.ClassName);
    }

    [Theory]
    [InlineData("inbox", "/inbox")]
    [InlineData("areas/0b8c1c7e-0000-0000-0000-000000000001", "/areas")]
    [InlineData("lists/0b8c1c7e-0000-0000-0000-000000000001", "/areas")]
    [InlineData("goals", "/goals")]
    [InlineData("budgets", "/budgets")]
    public void TheCurrentScreensTabIsMarked(string path, string tabHref)
    {
        Arrange();
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo(path);

        var nav = Render<BottomNav>();

        var active = nav.Find(".pspad-tab-active");
        Assert.Equal(tabHref, active.GetAttribute("href"));
        Assert.Equal("page", active.GetAttribute("aria-current"));
    }

    [Fact]
    public void MyDayIsTheRaisedCentreButtonAndMarkedOnItsOwnScreen()
    {
        Arrange();

        var nav = Render<BottomNav>();

        var link = nav.Find("a.pspad-tab-myday");
        Assert.Equal("/", link.GetAttribute("href"));
        Assert.Contains("pspad-tab-active", link.ClassList);
    }

    [Fact]
    public void TheMyDayLabelIsInsideTheLinkAndNotAnnouncedTwice()
    {
        Arrange();

        var nav = Render<BottomNav>();

        var link = nav.Find("a.pspad-tab-myday");
        Assert.NotNull(link.QuerySelector(".pspad-tab-label"));
        Assert.Equal("true", nav.Find(".pspad-myday-fab").GetAttribute("aria-hidden"));
    }

    [Fact]
    public void NoTabIsMarkedOnSettings()
    {
        Arrange();
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("settings");

        var nav = Render<BottomNav>();

        Assert.Empty(nav.FindAll(".pspad-tab-active"));
    }

    [Fact]
    public async Task TheInboxBadgeCountsItemsAndHidesWhenEmpty()
    {
        var replica = Arrange();
        var nav = Render<BottomNav>();
        Assert.Empty(nav.FindAll(".mud-badge-badge"));

        await replica.SaveAsync(InboxWith("milk", "bread", "eggs"));
        await nav.InvokeAsync(() => Services.GetRequiredService<SidebarCounts>().RefreshAsync());

        nav.WaitForAssertion(() => Assert.Contains("3", nav.Find(".mud-badge").TextContent));
    }

    [Fact]
    public void ItFollowsNavigation()
    {
        Arrange();
        var nav = Render<BottomNav>();

        Services.GetRequiredService<BunitNavigationManager>().NavigateTo("goals");

        nav.WaitForAssertion(() => Assert.Equal("/goals", nav.Find(".pspad-tab-active").GetAttribute("href")));
    }

    InMemoryReplica Arrange()
    {
        var today = new DateOnly(2026, 9, 12);
        var replica = AppTestHost.Arrange(this, User, today);
        Services.AddSingleton(new SidebarCounts(
            new ReplicaDocumentStore<TodoTask>(replica),
            new ReplicaDocumentStore<Inbox>(replica),
            new AppState { UserId = User, Today = today }));
        return replica;
    }

    static Inbox InboxWith(params string[] texts)
    {
        var inbox = new Inbox();
        var inboxId = Guid.NewGuid();
        inbox.ApplyAll(Inbox.Decide(null, new CreateInbox(Guid.NewGuid(), User, inboxId), DateTimeOffset.UnixEpoch));
        foreach (var text in texts)
        {
            inbox.ApplyAll(Inbox.Decide(inbox,
                new CaptureToInbox(Guid.NewGuid(), User, inboxId, Guid.NewGuid(), text), DateTimeOffset.UnixEpoch));
        }

        return inbox;
    }

    [Fact]
    public void TheSlotsAreInboxAreasMyDayGoalsBudgets()
    {
        Arrange();

        var nav = Render<BottomNav>();

        var hrefs = nav.FindAll(".pspad-bottom-nav-slots > a, .pspad-bottom-nav-slots > .mud-button-root")
            .Select(slot => slot.GetAttribute("href"));
        Assert.Equal(["/inbox", "/areas", "/", "/goals", "/budgets"], hrefs);
    }
}
