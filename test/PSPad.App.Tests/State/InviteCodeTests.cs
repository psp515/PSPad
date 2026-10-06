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
    public void TheQrLinkCarriesTheCodeInTheFragment() =>
        Assert.Equal("https://pspad.home/join/tok#code=K7M4PX", InviteCode.QrLinkFor("https://pspad.home/", "tok", "K7M4PX"));
}
