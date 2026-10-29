using PSPad.App.State;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class SnapshotVisitRowsTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheNewestVisitComesFirst()
    {
        var rows = SnapshotVisitRows.Live(
        [
            new SnapshotVisitView("tok-old", "Old list", Now.AddDays(5), Now.AddDays(-2)),
            new SnapshotVisitView("tok-new", "New list", Now.AddDays(5), Now.AddDays(-1))
        ], Now);

        Assert.Equal(["New list", "Old list"], rows.Select(row => row.Name));
    }

    [Fact]
    public void AnExpiredSnapshotIsDropped()
    {
        var rows = SnapshotVisitRows.Live(
        [
            new SnapshotVisitView("tok-expired", "Expired list", Now.AddDays(-1), Now.AddDays(-3)),
            new SnapshotVisitView("tok-live", "Live list", Now.AddDays(1), Now.AddDays(-3))
        ], Now);

        Assert.Equal(["Live list"], rows.Select(row => row.Name));
    }

    [Fact]
    public void ACachedSnapshotIsListedByWhenItWasOpenedNotWhenItWasPublished()
    {
        var publishedAt = Now.AddDays(-10);
        var snapshot = new SnapshotView(Guid.NewGuid(), "Cached list", "Tasks", publishedAt, Now.AddDays(5), [], []);

        var row = Assert.Single(SnapshotVisitRows.FromCache([("tok-cached", snapshot, Now)]));

        Assert.Equal(new SnapshotVisitView("tok-cached", "Cached list", Now.AddDays(5), Now), row);
    }
}
