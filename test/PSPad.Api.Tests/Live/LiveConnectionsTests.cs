using Microsoft.Extensions.Options;
using PSPad.Api.Live;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Live;

[UnitTest]
public class LiveConnectionsTests
{
    static LiveConnections Connections(int cap = 10) =>
        new(Options.Create(new LiveOptions { MaxStreamsPerUser = cap }));

    [Fact]
    public void ANudgeReachesEveryStreamOfThatUserAndNoOneElse()
    {
        var connections = Connections();
        var user = Guid.NewGuid();
        using var phone = connections.Open(user)!;
        using var laptop = connections.Open(user)!;
        using var stranger = connections.Open(Guid.NewGuid())!;

        connections.Nudge([user]);

        Assert.True(phone.Nudges.TryRead(out _));
        Assert.True(laptop.Nudges.TryRead(out _));
        Assert.False(stranger.Nudges.TryRead(out _));
    }

    [Fact]
    public void NudgesBetweenTwoReadsCollapseIntoOne()
    {
        var connections = Connections();
        var user = Guid.NewGuid();
        using var stream = connections.Open(user)!;

        connections.Nudge([user]);
        connections.Nudge([user]);
        connections.Nudge([user]);

        Assert.True(stream.Nudges.TryRead(out _));
        Assert.False(stream.Nudges.TryRead(out _));
    }

    [Fact]
    public void AStreamPastTheCapIsRefused()
    {
        var connections = Connections(cap: 2);
        var user = Guid.NewGuid();
        using var first = connections.Open(user);
        using var second = connections.Open(user);

        Assert.Null(connections.Open(user));
    }

    [Fact]
    public void ClosingAStreamFreesItsPlace()
    {
        var connections = Connections(cap: 1);
        var user = Guid.NewGuid();
        var stream = connections.Open(user)!;

        stream.Dispose();
        stream.Dispose();

        Assert.Equal(0, connections.OpenCount(user));
        Assert.True(connections.IsEmpty);
        using var again = connections.Open(user);
        Assert.NotNull(again);
    }

    [Fact]
    public void NudgingAUserWithNoStreamIsANoOp()
    {
        var connections = Connections();

        connections.Nudge([Guid.NewGuid()]);

        Assert.True(connections.IsEmpty);
    }
}
