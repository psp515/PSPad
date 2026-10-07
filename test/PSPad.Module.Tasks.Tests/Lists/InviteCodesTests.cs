using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Lists;

[UnitTest]
public class InviteCodesTests
{
    [Theory]
    [InlineData("k7m-4px", "K7M4PX")]
    [InlineData(" K7M 4PX ", "K7M4PX")]
    [InlineData(null, "")]
    public void NormalizeUppercasesAndDropsSeparators(string? input, string expected) =>
        Assert.Equal(expected, InviteCodes.Normalize(input));

    [Theory]
    [InlineData("K7M4PX", true)]
    [InlineData("K7M4P", false)]
    [InlineData("K7M4P0", true)]
    [InlineData("O01ILZ", true)]
    [InlineData("K7M4P!", false)]
    [InlineData("K7M4PÄ", false)]
    public void OnlySixUppercaseLettersOrDigitsAreWellFormed(string code, bool expected) =>
        Assert.Equal(expected, InviteCodes.IsWellFormed(code));

    [Fact]
    public void FormatSplitsIntoTwoGroups() => Assert.Equal("K7M-4PX", InviteCodes.Format("K7M4PX"));
}
