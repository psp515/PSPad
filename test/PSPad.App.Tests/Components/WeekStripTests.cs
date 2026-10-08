using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class WeekStripTests : Bunit.TestContext
{
    static readonly DateOnly Thursday = new(2026, 10, 8);

    public WeekStripTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    [Fact]
    public void ItShowsMondayToSundayOfThePickedWeek()
    {
        var strip = Strip(Thursday, Thursday);

        var days = strip.FindAll(".pspad-week-day");
        Assert.Equal(["5", "6", "7", "8", "9", "10", "11"], days.Select(day => day.QuerySelector(".pspad-week-circle")!.TextContent.Trim()));
        Assert.Equal(["M", "T", "W", "T", "F", "S", "S"], days.Select(day => day.QuerySelector(".pspad-week-initial")!.TextContent.Trim()));
        Assert.Equal("Mon, 5 Oct", days[0].GetAttribute("aria-label"));
        Assert.Equal("Thu, 8 Oct", days[3].GetAttribute("aria-label"));
    }

    [Fact]
    public void ASundayBelongsToTheWeekThatEndsWithIt()
    {
        var strip = Strip(new DateOnly(2026, 10, 11), Thursday);

        Assert.Equal("Mon, 5 Oct", strip.FindAll(".pspad-week-day")[0].GetAttribute("aria-label"));
    }

    [Theory]
    [InlineData(".pspad-week-prev", -7)]
    [InlineData(".pspad-week-next", 7)]
    public void TheArrowsMoveByAWeek(string arrow, int days)
    {
        DateOnly? picked = null;
        var strip = Strip(Thursday, Thursday, day => picked = day);

        strip.Find(arrow).Click();

        Assert.Equal(Thursday.AddDays(days), picked);
    }

    [Fact]
    public void TheArrowsAreLabelled()
    {
        var strip = Strip(Thursday, Thursday);

        Assert.Equal("Previous week", strip.Find(".pspad-week-prev").GetAttribute("aria-label"));
        Assert.Equal("Next week", strip.Find(".pspad-week-next").GetAttribute("aria-label"));
        Assert.Equal("Pick a date", strip.Find(".pspad-week-pick").GetAttribute("aria-label"));
    }

    [Fact]
    public void TappingADayPicksIt()
    {
        DateOnly? picked = null;
        var strip = Strip(Thursday, Thursday, day => picked = day);

        strip.FindAll(".pspad-week-day")[5].Click();

        Assert.Equal(new DateOnly(2026, 10, 10), picked);
    }

    [Fact]
    public void TheTodayButtonShowsOnlyAwayFromToday()
    {
        Assert.Empty(Strip(Thursday, Thursday).FindAll(".pspad-week-today"));

        DateOnly? picked = null;
        var strip = Strip(Thursday.AddDays(9), Thursday, day => picked = day);
        strip.Find(".pspad-week-today").Click();

        Assert.Equal(Thursday, picked);
    }

    [Fact]
    public void TodayIsRingedAndThePickedDayIsFilled()
    {
        var strip = Strip(Thursday.AddDays(1), Thursday);

        var days = strip.FindAll(".pspad-week-day");
        Assert.Contains("pspad-week-day-today", days[3].ClassName);
        Assert.Equal("date", days[3].GetAttribute("aria-current"));
        Assert.DoesNotContain("pspad-week-day-picked", days[3].ClassName);
        Assert.Contains("pspad-week-day-picked", days[4].ClassName);
        Assert.Null(days[4].GetAttribute("aria-current"));
        Assert.Single(strip.FindAll(".pspad-week-day-picked"));
    }

    [Fact]
    public void BusyDaysGetADot()
    {
        var strip = Strip(Thursday, Thursday, busy: new HashSet<DateOnly> { Thursday, Thursday.AddDays(2) });

        var days = strip.FindAll(".pspad-week-day");
        Assert.Equal([3, 5], days.Select((day, index) => (day, index))
            .Where(pair => pair.day.QuerySelector(".pspad-week-dot") is not null)
            .Select(pair => pair.index));
    }

    [Fact]
    public void TheHeaderNamesARelativeDayAndItsWeek()
    {
        var strip = Strip(Thursday, Thursday);

        Assert.Equal("Today · Thu, 8 Oct", strip.Find(".pspad-week-label").TextContent.Trim());
        Assert.Equal("Week 41 · October 2026", strip.Find(".pspad-week-meta").TextContent.Trim());
        Assert.Equal("October 2026", strip.Find(".pspad-week-month").TextContent.Trim());
    }

    [Fact]
    public void TheHeaderNamesAFarDayAlone()
    {
        var strip = Strip(new DateOnly(2026, 11, 2), Thursday);

        Assert.Equal("Mon, 2 Nov", strip.Find(".pspad-week-label").TextContent.Trim());
        Assert.Equal("Week 45 · November 2026", strip.Find(".pspad-week-meta").TextContent.Trim());
    }

    [Fact]
    public void AWeekAcrossTheNewYearBelongsToItsIsoWeek()
    {
        var strip = Strip(new DateOnly(2027, 1, 3), Thursday);

        Assert.Equal("Mon, 28 Dec", strip.FindAll(".pspad-week-day")[0].GetAttribute("aria-label"));
        Assert.Equal("Week 53 · January 2027", strip.Find(".pspad-week-meta").TextContent.Trim());
    }

    [Theory]
    [InlineData(2026, 10, 9, "Tomorrow · Fri, 9 Oct")]
    [InlineData(2026, 11, 2, "Mon, 2 Nov")]
    [InlineData(2027, 1, 4, "Mon, 4 Jan 2027")]
    public void EverythingTheStripSaysIsInOneCulture(int year, int month, int day, string label)
    {
        var current = CultureInfo.CurrentCulture;
        var currentUi = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("pl-PL");
        try
        {
            var strip = Strip(new DateOnly(year, month, day), Thursday);

            Assert.Equal(label, strip.Find(".pspad-week-label").TextContent.Trim());
            Assert.StartsWith("Mon, ", strip.FindAll(".pspad-week-day")[0].GetAttribute("aria-label"));
            Assert.Equal("M", strip.FindAll(".pspad-week-initial")[0].TextContent.Trim());
            Assert.DoesNotContain("październik", strip.Markup, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CultureInfo.CurrentCulture = current;
            CultureInfo.CurrentUICulture = currentUi;
        }
    }

    [Fact]
    public void SwipingLeftShowsTheNextWeek()
    {
        DateOnly? picked = null;
        var strip = Strip(Thursday, Thursday, day => picked = day);
        var days = strip.Find(".pspad-week-days");

        days.TouchStart(new Microsoft.AspNetCore.Components.Web.TouchEventArgs { Touches = [new() { ClientX = 200, ClientY = 10 }] });
        days.TouchEnd(new Microsoft.AspNetCore.Components.Web.TouchEventArgs { ChangedTouches = [new() { ClientX = 100, ClientY = 20 }] });

        Assert.Equal(Thursday.AddDays(7), picked);
    }

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
