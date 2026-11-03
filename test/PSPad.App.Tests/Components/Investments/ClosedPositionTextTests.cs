using PSPad.App.Components.Investments;
using PSPad.App.Resources;
using PSPad.App.Tests.Localization;
using PSPad.App.Tests.State;
using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components.Investments;

[UnitTest]
public class ClosedPositionTextTests
{
    static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    static ClosedPosition AClosed(DateTimeOffset? bought) =>
        new("DEF.PL", 10m, 160m, 150m, bought, new DateTimeOffset(2026, 9, 30, 22, 30, 0, TimeSpan.Zero), []);

    [Fact]
    public void DatesRunFromTheFirstPurchaseToTheLastSaleInTheUsersZone()
    {
        using var culture = new CultureScope("en-US");

        var text = ClosedPositionText.For(AClosed(new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero)), Warsaw,
            TestLocalizers.For<InvestmentsStrings>());

        Assert.Equal("Sold 10 · 1 Sep 2026 → 1 Oct 2026", text.Sub);
        Assert.Equal(-0.0625m, text.Share);
    }

    [Fact]
    public void ASaleWithoutAPurchaseShowsOnlyItsDate()
    {
        using var culture = new CultureScope("pl-PL");

        var text = ClosedPositionText.For(AClosed(null), Warsaw, TestLocalizers.For<InvestmentsStrings>());

        Assert.Equal("Sprzedano 10 · → 1 paź 2026", text.Sub);
    }
}
