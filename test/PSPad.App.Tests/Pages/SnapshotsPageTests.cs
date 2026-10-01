using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Api;
using PSPad.App.Components;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.App.Sync;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class SnapshotsPageTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void OnlineListsVisitsNewestFirst()
    {
        var now = Now();
        var api = new FakeSnapshotsApi
        {
            Visits =
            [
                new SnapshotVisitView("tok-old", "Old list", now.AddDays(5), now.AddDays(-2)),
                new SnapshotVisitView("tok-new", "New list", now.AddDays(5), now.AddDays(-1))
            ]
        };
        Arrange(api, online: true);

        var page = Render();

        var markup = page.Markup;
        Assert.True(markup.IndexOf("New list") < markup.IndexOf("Old list"));
    }

    [Fact]
    public async Task OfflineShowsCachedSnapshots()
    {
        var now = Now();
        var cache = new InMemorySnapshotCache();
        await cache.SaveAsync("tok-cached", new SnapshotView(
            Guid.NewGuid(), "Cached list", "Tasks", now.AddDays(-3), now.AddDays(5), [], []));
        Arrange(new FakeSnapshotsApi(), online: false, cache: cache);

        var page = Render();

        Assert.Contains("Cached list", page.Markup);
    }

    [Fact]
    public async Task FailingOnlineCallFallsBackToCache()
    {
        var now = Now();
        var cache = new InMemorySnapshotCache();
        await cache.SaveAsync("tok-cached", new SnapshotView(
            Guid.NewGuid(), "Cached list", "Tasks", now.AddDays(-3), now.AddDays(5), [], []));
        Arrange(new FakeSnapshotsApi { Throws = true }, online: true, cache: cache);

        var page = Render();

        Assert.Contains("Cached list", page.Markup);
    }

    [Fact]
    public async Task ExpiredCachedSnapshotsAreDropped()
    {
        var now = Now();
        var cache = new InMemorySnapshotCache();
        await cache.SaveAsync("tok-expired", new SnapshotView(
            Guid.NewGuid(), "Expired list", "Tasks", now.AddDays(-10), now.AddDays(-1), [], []));
        Arrange(new FakeSnapshotsApi(), online: false, cache: cache);

        var page = Render();

        Assert.DoesNotContain("Expired list", page.Markup);
    }

    [Fact]
    public void EmptyShowsEmptyStateWithNoCreateButton()
    {
        Arrange(new FakeSnapshotsApi(), online: true);

        var page = Render();

        var emptyState = page.FindComponent<EmptyState>();
        Assert.Contains("Snapshots you open while signed in show up here.", page.Markup);
        Assert.False(emptyState.Instance.OnCreate.HasDelegate);
    }

    [Fact]
    public void RowShowsNameExpiresAndOpenedAndLinksToTheSnapshot()
    {
        var now = Now();
        var api = new FakeSnapshotsApi
        {
            Visits = [new SnapshotVisitView("tok-1", "Weekly list", now.AddDays(5), now.AddDays(-1))]
        };
        Arrange(api, online: true);

        var page = Render();

        Assert.Contains("Weekly list", page.Markup);
        Assert.Contains("Expires", page.Markup);
        Assert.Contains("Opened", page.Markup);
        Assert.Contains("/s/tok-1", page.Markup);
    }

    IRenderedComponent<SnapshotsPage> Render() => Render<SnapshotsPage>();

    void Arrange(FakeSnapshotsApi api, bool online, InMemorySnapshotCache? cache = null)
    {
        AppTestHost.Arrange(this, User, Today);
        Services.AddSingleton<ISnapshotsApi>(api);
        Services.AddSingleton<IConnectivity>(new ToggleableConnectivity(online));
        Services.AddSingleton<ISnapshotCache>(cache ?? new InMemorySnapshotCache());
    }

    static DateTimeOffset Now() => new(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    sealed class ToggleableConnectivity(bool isOnline) : IConnectivity
    {
        public bool IsOnline { get; } = isOnline;

#pragma warning disable CS0067
        public event Action? CameOnline;

        public event Action? Changed;
#pragma warning restore CS0067
    }

    sealed class FakeSnapshotsApi : ISnapshotsApi
    {
        public IReadOnlyList<SnapshotVisitView> Visits { get; set; } = [];

        public bool Throws { get; set; }

        public Task<PublishedSnapshotView?> PublishAsync(Guid listId, DateTimeOffset expiresAt) =>
            Task.FromResult<PublishedSnapshotView?>(null);

        public Task<IReadOnlyList<PublishedSnapshotView>> ForListAsync(Guid listId) =>
            Task.FromResult<IReadOnlyList<PublishedSnapshotView>>([]);

        public Task<bool> RevokeAsync(Guid snapshotId) => Task.FromResult(false);

        public Task<bool> RecordVisitAsync(string token) => Task.FromResult(false);

        public Task<IReadOnlyList<SnapshotVisitView>> VisitsAsync() =>
            Throws ? throw new HttpRequestException() : Task.FromResult(Visits);
    }
}
