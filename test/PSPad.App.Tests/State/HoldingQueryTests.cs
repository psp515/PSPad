using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class HoldingQueryTests
{
    static readonly Guid SnapshotId = Guid.NewGuid();

    [Fact]
    public void AHoldingRoundTrips()
    {
        var holdingId = Guid.NewGuid();

        var uri = HoldingQuery.For("http://localhost/budgets/x/balance?entry=1", SnapshotId, holdingId);

        Assert.Equal($"http://localhost/budgets/x/balance?balance={SnapshotId}&holding={holdingId}", uri);
        Assert.Equal<(Guid, Guid?)?>((SnapshotId, holdingId), HoldingQuery.From(uri));
    }

    [Theory]
    [InlineData("http://localhost/budgets/x/balance")]
    [InlineData("http://localhost/budgets/x/balance?holding=new")]
    [InlineData("http://localhost/budgets/x/balance?balance=nope&holding=new")]
    public void AnythingElseOpensNothing(string uri) => Assert.Null(HoldingQuery.From(uri));

    [Fact]
    public void AnUnreadableHoldingOpensNothing()
    {
        Assert.Null(HoldingQuery.From($"http://localhost/budgets/x/balance?balance={SnapshotId}&holding=nope"));
        Assert.Null(HoldingQuery.From($"http://localhost/budgets/x/balance?balance={SnapshotId}"));
    }
}
