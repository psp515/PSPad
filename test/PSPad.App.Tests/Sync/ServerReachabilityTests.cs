using PSPad.App.Sync;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Sync;

[UnitTest]
public class ServerReachabilityTests
{
    [Fact]
    public void ItStartsOutAssumingTheServerIsReachable()
    {
        Assert.True(new ServerReachability().IsReachable);
    }

    [Fact]
    public void ItReportsTheServerUnreachableAfterAFailureWhileTheBrowserIsOnline()
    {
        var reachability = new ServerReachability();

        reachability.Failed(browserIsOnline: true);

        Assert.False(reachability.IsReachable);
    }

    [Fact]
    public void AFailureWhileTheBrowserIsOfflineIsExpectedAndLeavesTheServerReachable()
    {
        var reachability = new ServerReachability();

        reachability.Failed(browserIsOnline: false);

        Assert.True(reachability.IsReachable);
    }

    [Fact]
    public void ASuccessfulCallRestoresReachability()
    {
        var reachability = new ServerReachability();
        reachability.Failed(browserIsOnline: true);

        reachability.Succeeded();

        Assert.True(reachability.IsReachable);
    }

    [Fact]
    public void ItAnnouncesOnlyTheTransitionIntoUnreachable()
    {
        var reachability = new ServerReachability();
        var announcements = 0;
        reachability.Changed += () => announcements++;

        reachability.Failed(browserIsOnline: true);
        reachability.Failed(browserIsOnline: true);
        reachability.Failed(browserIsOnline: true);

        Assert.Equal(1, announcements);
    }

    [Fact]
    public void ItAnnouncesTheTransitionBackToReachable()
    {
        var reachability = new ServerReachability();
        reachability.Failed(browserIsOnline: true);
        var announcements = 0;
        reachability.Changed += () => announcements++;

        reachability.Succeeded();
        reachability.Succeeded();

        Assert.Equal(1, announcements);
    }
}
