using PSPad.App.State.Investments;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class InvestmentPriceTextTests
{
    [Theory]
    [InlineData(3.21000004, "3.21")]
    [InlineData(12.34499998, "12.34")]
    [InlineData(47.65500003, "47.66")]
    [InlineData(8.07999992, "8.08")]
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
        Assert.True(ChartScale.AxisWidthFor("8.08") >= 40);
        Assert.True(ChartScale.AxisWidthFor("1,234,567.00") > ChartScale.AxisWidthFor("8.08"));
        Assert.Equal(Math.Round(ChartScale.AxisWidthFor("12.34")), ChartScale.AxisWidthFor("12.34"));
    }
}
