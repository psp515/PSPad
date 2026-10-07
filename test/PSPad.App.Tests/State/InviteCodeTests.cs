using PSPad.App.State;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class InviteCodeTests
{
    [Fact]
    public void NewCodesAreWellFormed()
    {
        for (var i = 0; i < 200; i++)
        {
            Assert.True(InviteCodes.IsWellFormed(InviteCode.New()));
        }
    }

    [Fact]
    public void TheJoinLinkCarriesTheCodeInTheFragment() =>
        Assert.Equal("https://pspad.home/join/tok#code=K7M4PX", InviteCode.JoinLinkFor("https://pspad.home/", "tok", "K7M4PX"));
}
