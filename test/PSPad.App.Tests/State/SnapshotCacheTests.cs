using PSPad.App.State;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class SnapshotCacheTests
{
    [Fact]
    public async Task ASavedSnapshotComesBackByToken()
    {
        var cache = new InMemorySnapshotCache();
        var snapshot = Sample("tok1", DateTimeOffset.UtcNow.AddDays(1));

        await cache.SaveAsync("tok1", snapshot, DateTimeOffset.UtcNow);

        Assert.Equal(snapshot, await cache.GetAsync("tok1"));
    }

    [Fact]
    public async Task AnUnknownTokenComesBackNull()
    {
        var cache = new InMemorySnapshotCache();

        Assert.Null(await cache.GetAsync("nope"));
    }

    [Fact]
    public async Task AllAsyncListsEverySavedSnapshot()
    {
        var cache = new InMemorySnapshotCache();
        var first = Sample("tok1", DateTimeOffset.UtcNow.AddDays(1));
        var second = Sample("tok2", DateTimeOffset.UtcNow.AddDays(2));
        await cache.SaveAsync("tok1", first, DateTimeOffset.UtcNow);
        await cache.SaveAsync("tok2", second, DateTimeOffset.UtcNow);

        var all = await cache.AllAsync();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, entry => entry.Token == "tok1" && entry.Snapshot == first);
        Assert.Contains(all, entry => entry.Token == "tok2" && entry.Snapshot == second);
    }

    [Fact]
    public async Task AllAsyncCarriesTheVisitTimeSeparatelyFromTheSnapshotsCreatedAt()
    {
        var cache = new InMemorySnapshotCache();
        var publishedAt = DateTimeOffset.UtcNow.AddDays(-10);
        var openedAt = DateTimeOffset.UtcNow;
        var snapshot = new SnapshotView(
            Guid.NewGuid(), "List", "Tasks", publishedAt, publishedAt.AddDays(20), [], []);

        await cache.SaveAsync("tok1", snapshot, openedAt);

        var entry = Assert.Single(await cache.AllAsync());
        Assert.Equal(openedAt, entry.OpenedAt);
        Assert.NotEqual(snapshot.CreatedAt, entry.OpenedAt);
    }

    [Fact]
    public async Task SavingTheSameTokenAgainOverwritesIt()
    {
        var cache = new InMemorySnapshotCache();
        await cache.SaveAsync("tok1", Sample("tok1", DateTimeOffset.UtcNow.AddDays(1)), DateTimeOffset.UtcNow);
        var updated = Sample("tok1", DateTimeOffset.UtcNow.AddDays(5));

        await cache.SaveAsync("tok1", updated, DateTimeOffset.UtcNow);

        Assert.Equal(updated, await cache.GetAsync("tok1"));
    }

    [Fact]
    public async Task PruneAsyncDropsOnlyExpiredSnapshots()
    {
        var cache = new InMemorySnapshotCache();
        var now = DateTimeOffset.UtcNow;
        await cache.SaveAsync("expired", Sample("expired", now.AddDays(-1)), now);
        await cache.SaveAsync("live", Sample("live", now.AddDays(1)), now);

        await cache.PruneAsync(now);

        Assert.Null(await cache.GetAsync("expired"));
        Assert.NotNull(await cache.GetAsync("live"));
    }

    [Fact]
    public async Task ClearAsyncDropsEverything()
    {
        var cache = new InMemorySnapshotCache();
        await cache.SaveAsync("tok1", Sample("tok1", DateTimeOffset.UtcNow.AddDays(1)), DateTimeOffset.UtcNow);
        await cache.SaveAsync("tok2", Sample("tok2", DateTimeOffset.UtcNow.AddDays(1)), DateTimeOffset.UtcNow);

        await cache.ClearAsync();

        Assert.Empty(await cache.AllAsync());
    }

    static SnapshotView Sample(string token, DateTimeOffset expiresAt) =>
        new(Guid.NewGuid(), $"List {token}", "Tasks", DateTimeOffset.UtcNow, expiresAt, [], []);
}
