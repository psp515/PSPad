using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
[Collection("Culture")]
public class WeekStripTests : Bunit.TestContext
{
    readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

    static readonly DateOnly Thursday = new(2026, 10, 8);

    public WeekStripTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddLocalization();
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
    }

    protected override void Dispose(bool disposing)
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
        base.Dispose(disposing);
    }

    [Fact]
    public void ItShowsMondayToSundayOfThePickedWeek()
    {
        var strip = Strip(Thursday, Thursday);

        var days = Days(strip);
        Assert.Equal(["M5", "T6", "W7", "T8", "F9", "S10", "S11"], days.Select(day => Squash(day.TextContent)));
        Assert.Equal("Mon, 5 Oct", days[0].GetAttribute("aria-label"));
        Assert.Equal("Thu, 8 Oct", days[3].GetAttribute("aria-label"));
    }

    [Fact]
    public void ASundayBelongsToTheWeekThatEndsWithIt()
    {
        var strip = Strip(new DateOnly(2026, 10, 11), Thursday);

        Assert.Equal("Mon, 5 Oct", Days(strip)[0].GetAttribute("aria-label"));
    }

    [Theory]
    [InlineData("Previous week", -7)]
    [InlineData("Next week", 7)]
    public void TheArrowsMoveByAWeek(string arrow, int days)
    {
        DateOnly? picked = null;
        var strip = Strip(Thursday, Thursday, day => picked = day);

        strip.Find($"button[aria-label='{arrow}']").Click();

        Assert.Equal(Thursday.AddDays(days), picked);
    }

    [Fact]
    public void ThePickerButtonIsLabelled()
    {
        var strip = Strip(Thursday, Thursday);

        Assert.NotNull(strip.Find("button[aria-label='Pick a date']"));
    }

    [Fact]
    public void TappingADayPicksIt()
    {
        DateOnly? picked = null;
        var strip = Strip(Thursday, Thursday, day => picked = day);

        Days(strip)[5].Click();

        Assert.Equal(new DateOnly(2026, 10, 10), picked);
    }

    [Fact]
    public void TheTodayButtonShowsOnlyAwayFromToday()
    {
        Assert.DoesNotContain(Strip(Thursday, Thursday).FindAll("button"), button => button.TextContent.Trim() == "Today");

        DateOnly? picked = null;
        var strip = Strip(Thursday.AddDays(9), Thursday, day => picked = day);
        strip.FindAll("button").Single(button => button.TextContent.Trim() == "Today").Click();

        Assert.Equal(Thursday, picked);
    }

    [Fact]
    public void TodayIsMarkedAndThePickedDayIsPressed()
    {
        var strip = Strip(Thursday.AddDays(1), Thursday);

        var days = Days(strip);
        Assert.Equal("date", days[3].GetAttribute("aria-current"));
        Assert.Equal("false", days[3].GetAttribute("aria-pressed"));
        Assert.Equal("true", days[4].GetAttribute("aria-pressed"));
        Assert.Null(days[4].GetAttribute("aria-current"));
        Assert.Single(days, day => day.GetAttribute("aria-pressed") == "true");
    }

    [Fact]
    public void BusyDaysGetADot()
    {
        var strip = Strip(Thursday, Thursday, busy: new HashSet<DateOnly> { Thursday, Thursday.AddDays(2) });

        Assert.Equal([3, 5], Days(strip).Select((day, index) => (day, index))
            .Where(pair => pair.day.QuerySelector("[data-testid=week-day-busy]") is not null)
            .Select(pair => pair.index));
    }

    [Fact]
    public void TheHeaderNamesARelativeDayAndItsWeek()
    {
        var text = Strip(Thursday, Thursday).Text();

        Assert.Contains("Today · Thu, 8 Oct", text);
        Assert.Contains("Week 41 · October 2026", text);
    }

    [Fact]
    public void TheHeaderNamesAFarDayAlone()
    {
        var text = Strip(new DateOnly(2026, 11, 2), Thursday).Text();

        Assert.Contains("Mon, 2 Nov", text);
        Assert.DoesNotContain("Today ·", text);
        Assert.Contains("Week 45 · November 2026", text);
    }

    [Fact]
    public void AWeekAcrossTheNewYearBelongsToItsIsoWeek()
    {
        var strip = Strip(new DateOnly(2027, 1, 3), Thursday);

        Assert.Equal("Mon, 28 Dec", Days(strip)[0].GetAttribute("aria-label"));
        Assert.Contains("Week 53 · January 2027", strip.Text());
    }

    [Theory]
    [InlineData(2026, 10, 9, "Jutro · pt., 9 paź")]
    [InlineData(2026, 11, 2, "pon., 2 lis")]
    [InlineData(2027, 1, 4, "pon., 4 sty 2027")]
    public void EverythingTheStripSaysIsInOneCulture(int year, int month, int day, string label)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("pl-PL");

        var strip = Strip(new DateOnly(year, month, day), Thursday);

        Assert.Contains(label, strip.Text());
        Assert.StartsWith("pon.", Days(strip)[0].GetAttribute("aria-label"));
        Assert.DoesNotContain("Oct", strip.Text());
    }

    [Fact]
    public void TheWeekStripStartsOnMondayInPolishAndNamesMonthsInPolish()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");

        var strip = Strip(new DateOnly(2026, 10, 18), new DateOnly(2026, 10, 18));

        Assert.Contains("październik 2026", strip.Text());
        var first = Squash(Days(strip)[0].TextContent);
        Assert.StartsWith("p", first.ToLowerInvariant());
        Assert.EndsWith("12", first);
    }

    [Fact]
    public void TheWeekStartsOnSundayWhereTheCultureSaysSo()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

        var strip = Strip(Thursday, Thursday);

        var days = Days(strip);
        Assert.EndsWith("4", Squash(days[0].TextContent));
        Assert.EndsWith("10", Squash(days[6].TextContent));
    }

    [Fact]
    public void SwipingLeftShowsTheNextWeek()
    {
        DateOnly? picked = null;
        var strip = Strip(Thursday, Thursday, day => picked = day);
        var day = Days(strip)[3];

        day.TouchStart(new Microsoft.AspNetCore.Components.Web.TouchEventArgs { Touches = [new() { ClientX = 200, ClientY = 10 }] });
        day.TouchEnd(new Microsoft.AspNetCore.Components.Web.TouchEventArgs { ChangedTouches = [new() { ClientX = 100, ClientY = 20 }] });

        Assert.Equal(Thursday.AddDays(7), picked);
    }

    static IReadOnlyList<AngleSharp.Dom.IElement> Days(IRenderedComponent<WeekStrip> strip) => strip.FindAll("button[aria-pressed]");

    static string Squash(string text) => string.Concat(text.Where(character => !char.IsWhiteSpace(character)));

    [Theory]
    [InlineData(-80, 10, 1)]
    [InlineData(80, -10, -1)]
    [InlineData(-47, 0, 0)]
    [InlineData(30, 0, 0)]
    [InlineData(-60, 90, 0)]
    [InlineData(60, -60, 0)]
    public void ASwipeTurnsTheWeekOnlyWhenItIsLongAndMostlySideways(double dx, double dy, int expected) =>
        Assert.Equal(expected, WeekStrip.SwipeDirection(dx, dy));

    IRenderedComponent<WeekStrip> Strip(DateOnly day, DateOnly today, Action<DateOnly>? changed = null, IReadOnlySet<DateOnly>? busy = null) =>
        Render<WeekStrip>(p => p
            .Add(s => s.Day, day)
            .Add(s => s.Today, today)
            .Add(s => s.Busy, busy ?? new HashSet<DateOnly>())
            .Add(s => s.DayChanged, (DateOnly picked) => changed?.Invoke(picked)));
}
