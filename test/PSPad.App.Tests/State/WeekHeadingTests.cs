using PSPad.App.Resources;
using PSPad.App.State;
using PSPad.App.Tests.Localization;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
[Collection("Culture")]
public class WeekHeadingTests : IDisposable
{
    static readonly DateOnly Thursday = new(2026, 10, 8);

    readonly CultureScope _culture = new("en-GB");

    public void Dispose() => _culture.Dispose();

    [Fact]
    public void ANearbyDayIsNamedRelativelyWithItsWeek()
    {
        Assert.Equal("Today · Thu, 8 Oct", Label(Thursday));
        Assert.Equal("Tomorrow · Fri, 9 Oct", Label(Thursday.AddDays(1)));
        Assert.Equal("Week 41 · October 2026", Meta(Thursday));
    }

    [Fact]
    public void AFarDayIsNamedAlone()
    {
        Assert.Equal("Mon, 2 Nov", Label(new DateOnly(2026, 11, 2)));
        Assert.Equal("Week 45 · November 2026", Meta(new DateOnly(2026, 11, 2)));
    }

    [Fact]
    public void ADayInAnotherYearCarriesItsYearAndItsIsoWeek()
    {
        Assert.Equal("Sun, 3 Jan 2027", Label(new DateOnly(2027, 1, 3)));
        Assert.Equal("Week 53 · January 2027", Meta(new DateOnly(2027, 1, 3)));
    }

    [Theory]
    [InlineData(2026, 10, 9, "Jutro · pt., 9 paź")]
    [InlineData(2026, 11, 2, "pon., 2 lis")]
    [InlineData(2027, 1, 4, "pon., 4 sty 2027")]
    public void EverythingTheHeadingSaysIsInOneCulture(int year, int month, int day, string label)
    {
        CultureScope.Use("pl-PL");

        Assert.Equal(label, Label(new DateOnly(year, month, day)));
        Assert.Equal("Tydzień 42 · październik 2026", Meta(new DateOnly(2026, 10, 18)));
    }

    static string Label(DateOnly day) => WeekHeading.Label(day, Thursday, TestLocalizers.For<SharedStrings>());

    static string Meta(DateOnly day) => WeekHeading.Meta(day, TestLocalizers.For<TodayStrings>());
}
