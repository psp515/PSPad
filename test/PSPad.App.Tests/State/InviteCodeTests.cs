using PSPad.App.State;
using PSPad.Abstractions;
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
}
