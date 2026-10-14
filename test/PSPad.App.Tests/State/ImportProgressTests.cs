using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class ImportProgressTests
{
    [Theory]
    [InlineData(1, 100, false)]
    [InlineData(24, 100, false)]
    [InlineData(25, 100, true)]
    [InlineData(26, 100, false)]
    [InlineData(50, 100, true)]
    [InlineData(100, 100, true)]
    [InlineData(7, 7, true)]
    public void OnlyEveryTwentyFifthCommandAndTheLastAreDue(int done, int total, bool expected)
    {
        Assert.Equal(expected, ImportProgress.IsDue(done, total));
    }
}
