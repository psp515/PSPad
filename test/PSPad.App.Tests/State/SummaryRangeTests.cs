using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class SummaryRangeTests
{
    static readonly DateOnly Today = new(2026, 10, 9);

    [Fact]
    public void OnNewYearsDayThisYearIsOneDay()
    {
        var newYear = new DateOnly(2027, 1, 1);

        Assert.Equal(new SummaryRange(newYear, newYear), SummaryRange.For(SummaryPreset.ThisYear, newYear));
    }

    [Fact]
    public void TheLastSixMonthsCrossTheYearBoundary() =>
        Assert.Equal(new SummaryRange(new DateOnly(2025, 8, 1), new DateOnly(2026, 1, 31)),
            SummaryRange.For(SummaryPreset.LastSixMonths, new DateOnly(2026, 2, 14)));

    [Fact]
    public void ACustomRangeHasNoPresetDays() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SummaryRange.For(SummaryPreset.Custom, Today));

    [Fact]
    public void APickedRangeKeepsItsDaysInOrder() =>
        Assert.Equal(new SummaryRange(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 9)),
            SummaryRange.Picked(new DateTime(2026, 3, 9, 13, 0, 0), new DateTime(2026, 3, 2)));

    [Fact]
    public void AHalfPickedRangeIsNoRange()
    {
        Assert.Null(SummaryRange.Picked(new DateTime(2026, 3, 2), null));
        Assert.Null(SummaryRange.Picked(null, new DateTime(2026, 3, 2)));
        Assert.Null(SummaryRange.Picked(null, null));
    }
}
