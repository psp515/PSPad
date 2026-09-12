using PSPad.Abstractions;
using PSPad.Module.Tasks.Ordering;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests;

[UnitTest]
public class PositionsTests
{
    [Fact]
    public void NextIsZeroWhenNothingIsTaken()
    {
        Assert.Equal(0, Positions.Next([]));
    }

    [Fact]
    public void NextFollowsTheHighestTakenPosition()
    {
        Assert.Equal(3, Positions.Next([0, 1, 2]));
    }

    [Fact]
    public void MoveShiftsTheItemAndLeavesNoGaps()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        var moved = Positions.Move([a, b, c], c, 0);

        Assert.Equal([c, a, b], moved);
    }

    [Fact]
    public void MoveClampsAnIndexPastTheEnd()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        Assert.Equal([b, a], Positions.Move([a, b], a, 99));
    }

    [Fact]
    public void MoveRejectsAnItemThatIsNotInTheOrder()
    {
        Assert.Throws<DomainRejectedException>(
            () => Positions.Move([Guid.NewGuid()], Guid.NewGuid(), 0));
    }
}
