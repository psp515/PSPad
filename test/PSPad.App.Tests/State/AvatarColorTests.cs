using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class AvatarColorTests
{
    [Fact]
    public void TheSameUserAlwaysGetsTheSameColour()
    {
        var user = Guid.Parse("6f1d2c3b-0000-4000-8000-000000000001");

        Assert.Equal(AvatarColor.For(user), AvatarColor.For(user));
    }

    [Fact]
    public void AKnownUserAlwaysGetsAKnownColour()
    {
        var user = Guid.Parse("6f1d2c3b-0000-4000-8000-000000000001");

        Assert.Equal("#3E6E7A", AvatarColor.For(user));
    }

    [Fact]
    public void DifferentUsersCanGetDifferentColours()
    {
        var colours = Enumerable
            .Range(0, 40)
            .Select(_ => AvatarColor.For(Guid.NewGuid()))
            .Distinct()
            .Count();

        Assert.True(colours > 1);
    }

    [Fact]
    public void EveryColourIsAHexTriplet()
    {
        var colour = AvatarColor.For(Guid.NewGuid());

        Assert.Matches("^#[0-9A-F]{6}$", colour);
    }

    [Theory]
    [InlineData("ada@example.com", "A")]
    [InlineData("  ada@example.org", "A")]
    [InlineData("", "?")]
    public void TheInitialIsTheFirstLetterOfTheAddress(string email, string expected)
    {
        Assert.Equal(expected, AvatarColor.InitialOf(email));
    }
}
