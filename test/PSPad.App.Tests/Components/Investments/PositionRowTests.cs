using PSPad.App.Components.Investments;
using PSPad.App.Resources;
using PSPad.App.Tests.Localization;
using PSPad.App.Tests.State;
using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components.Investments;

[UnitTest]
public class PositionRowTests
{
    static readonly Guid Account = Guid.NewGuid();

    static Position APosition(PositionWarning warning, decimal oversoldBy = 0m) =>
        new(Account, "ABC.US", 3m, 300m, 100m, warning == PositionWarning.NoPrice ? null : 90m,
            warning == PositionWarning.NoPrice ? null : new DateOnly(2026, 10, 28), 270m, -30m, [], warning, oversoldBy);

    [Fact]
    public void APositionWithNoPriceOffersToFixItsSymbol()
    {
        using var culture = new CultureScope("en-US");

        var text = PositionText.For(APosition(PositionWarning.NoPrice), TestLocalizers.For<InvestmentsStrings>());

        Assert.True(text.OffersFixSymbol);
        Assert.Equal("No price", text.Warning);
    }

    [Fact]
    public void APricedPositionOffersNoFixAndNoWarning()
    {
        var text = PositionText.For(APosition(PositionWarning.None), TestLocalizers.For<InvestmentsStrings>());

        Assert.False(text.OffersFixSymbol);
        Assert.Null(text.Warning);
    }

    [Fact]
    public void AnOversoldPositionSaysByHowMuch()
    {
        using var culture = new CultureScope("en-US");

        var text = PositionText.For(APosition(PositionWarning.Oversold, 2.5m), TestLocalizers.For<InvestmentsStrings>());

        Assert.Equal("Oversold by 2.5", text.Warning);
    }

    [Fact]
    public void UnrealizedIsAShareOfTheOpenCost()
    {
        var text = PositionText.For(APosition(PositionWarning.None), TestLocalizers.For<InvestmentsStrings>());

        Assert.Equal(-0.1m, text.UnrealizedShare);
    }
}
