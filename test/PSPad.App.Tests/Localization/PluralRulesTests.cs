using System.Globalization;
using PSPad.App.Localization;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Localization;

[UnitTest]
public class PluralRulesTests
{
    static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");
    static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    [Theory]
    [InlineData(0, PluralForm.Many)]
    [InlineData(1, PluralForm.One)]
    [InlineData(2, PluralForm.Few)]
    [InlineData(4, PluralForm.Few)]
    [InlineData(5, PluralForm.Many)]
    [InlineData(12, PluralForm.Many)]
    [InlineData(14, PluralForm.Many)]
    [InlineData(22, PluralForm.Few)]
    [InlineData(25, PluralForm.Many)]
    [InlineData(102, PluralForm.Few)]
    public void PolishHasOneFewAndMany(int count, PluralForm expected)
    {
        Assert.Equal(expected, PluralRules.For(count, Polish));
    }

    [Theory]
    [InlineData(0, PluralForm.Many)]
    [InlineData(1, PluralForm.One)]
    [InlineData(2, PluralForm.Many)]
    public void EnglishHasOneAndMany(int count, PluralForm expected)
    {
        Assert.Equal(expected, PluralRules.For(count, English));
    }
}
