using PSPad.TestInfrastructure;
using PSPad.Module.Investments.Import;

namespace PSPad.Module.Investments.Tests.Import;

[UnitTest]
public class ExcelTimeTests
{
    [Fact]
    public void ExcelSerialsAreUtc() =>
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero), ExcelTime.ToUtc(46300.5));

    [Fact]
    public void AWholeSerialIsMidnightUtc() =>
        Assert.Equal(new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero), ExcelTime.ToUtc(2));
}
