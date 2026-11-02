using PSPad.App.State.Investments;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class InvestmentPriceTextTests
{
    [Theory]
    [InlineData(6.90100002, "6.90")]
    [InlineData(36.47399902, "36.47")]
    [InlineData(29.55944055, "29.56")]
    [InlineData(4.16000366, "4.16")]
    [InlineData(1, "1.00")]
    [InlineData(1234.5, "1,234.50")]
    [InlineData(0.5, "0.50")]
    [InlineData(0.123456789, "0.1235")]
    [InlineData(0.00123456, "0.001235")]
    [InlineData(-0.0456789, "-0.04568")]
    [InlineData(0, "0.00")]
    public void APriceShowsTwoDecimalsFromOneAndFourSignificantDigitsBelow(double price, string expected)
    {
        using var culture = new CultureScope("en-US");

        Assert.Equal(expected, InvestmentText.Price((decimal)price));
    }

    [Fact]
    public void ThePriceAxisIsWideEnoughForItsLongestLabel()
    {
        Assert.True(ChartScale.AxisWidthFor("1,234,567.00") >= 12 * 7);
        Assert.True(ChartScale.AxisWidthFor("4.16") >= 40);
        Assert.True(ChartScale.AxisWidthFor("1,234,567.00") > ChartScale.AxisWidthFor("4.16"));
        Assert.Equal(Math.Round(ChartScale.AxisWidthFor("12.34")), ChartScale.AxisWidthFor("12.34"));
    }
}
