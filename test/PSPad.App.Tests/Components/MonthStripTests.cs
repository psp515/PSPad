using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using PSPad.App.Components;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class MonthStripTests : Bunit.TestContext
{
    static readonly YearMonth October = new(2026, 10);

    YearMonth? _picked;

    public MonthStripTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddLocalization();
    }

    IRenderedComponent<ContainerFragment> Strip(YearMonth month, params YearMonth[] marked) => Render(builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<MonthStrip>(2);
        builder.AddAttribute(3, nameof(MonthStrip.Month), month);
        builder.AddAttribute(4, nameof(MonthStrip.Current), October);
        builder.AddAttribute(5, nameof(MonthStrip.Marked), (IReadOnlySet<YearMonth>)marked.ToHashSet());
        builder.AddAttribute(6, nameof(MonthStrip.OnMonthChanged),
            EventCallback.Factory.Create<YearMonth>(this, picked => _picked = picked));
        builder.CloseComponent();
    });

    static string Text(IRenderedComponent<ContainerFragment> strip, string selector) => strip.Find(selector).TextContent.Trim();

    static string[] Labels(IRenderedComponent<ContainerFragment> strip, string selector = ".pspad-month-chip") =>
        [.. strip.FindAll(selector).Select(chip => chip.GetAttribute("aria-label")!)];

    static void Resize(IRenderedComponent<ContainerFragment> strip, double width) =>
        strip.InvokeAsync(() => strip.FindComponent<MonthStrip>().Instance.Resized(width)).GetAwaiter().GetResult();

    [Fact]
    public void BeforeItIsMeasuredTheStripListsTwelveMonthsNewestFirst()
    {
        var strip = Strip(October);

        Assert.Equal(
        [
            "October 2026", "September 2026", "August 2026", "July 2026", "June 2026", "May 2026", "April 2026", "March 2026",
            "February 2026", "January 2026", "December 2025", "November 2025"
        ], Labels(strip));
        Assert.Equal(["Oct", "Sep", "Aug", "Jul", "Jun", "May", "Apr", "Mar", "Feb", "Jan", "Dec", "Nov"],
            strip.FindAll(".pspad-month-chip-label").Select(label => label.TextContent.Trim()));
    }

    [Theory]
    [InlineData(1200, 20)]
    [InlineData(599, 10)]
    [InlineData(300, 5)]
    [InlineData(60, 3)]
    public void TheStripShowsAsManyWholeMonthsAsFitItsWidth(double width, int chips)
    {
        var strip = Strip(October);

        Resize(strip, width);

        Assert.Equal(chips, strip.FindAll(".pspad-month-chip").Count);
        Assert.Equal("October 2026", strip.FindAll(".pspad-month-chip")[0].GetAttribute("aria-label"));
    }

    [Fact]
    public void JanuaryAndTheOldestChipShowTheirYear()
    {
        var strip = Strip(October);

        Assert.Equal(["January 2026", "November 2025"],
            strip.FindAll(".pspad-month-chip").Where(chip => chip.QuerySelector(".pspad-month-chip-year") is not null)
                .Select(chip => chip.GetAttribute("aria-label")));
        Assert.Equal(["2026", "2025"], strip.FindAll(".pspad-month-chip-year").Select(year => year.TextContent.Trim()));
    }

    [Fact]
    public void TheShownMonthIsPressedAndTheCurrentMonthIsRinged()
    {
        var strip = Strip(new YearMonth(2026, 9));

        Assert.Equal(["September 2026"], Labels(strip, ".pspad-month-chip[aria-pressed='true']"));
        Assert.Equal(["October 2026"], Labels(strip, ".pspad-month-chip-current"));
        Assert.Equal(["October 2026"], Labels(strip, ".pspad-month-chip[aria-current='date']"));
    }

    [Fact]
    public void MonthsWithDataCarryADot()
    {
        var strip = Strip(October, new YearMonth(2026, 8), new YearMonth(2025, 12));

        Assert.Equal(["August 2026", "December 2025"], Labels(strip, ".pspad-month-chip-marked"));
        Assert.Equal(12, strip.FindAll(".pspad-month-chip-dot").Count);
    }

    [Fact]
    public void TappingAChipShowsThatMonth()
    {
        var strip = Strip(October);

        strip.Find(".pspad-month-chip[aria-label='August 2026']").Click();

        Assert.Equal(new YearMonth(2026, 8), _picked);
    }

    [Fact]
    public void TheTodayButtonJumpsToTheCurrentMonth()
    {
        var strip = Strip(new YearMonth(2026, 3));
        Assert.Equal("This month", strip.Find(".pspad-month-today").GetAttribute("aria-label"));
        Assert.False(strip.Find(".pspad-month-today").HasAttribute("disabled"));

        strip.Find(".pspad-month-today").Click();

        Assert.Equal(October, _picked);
    }

    [Fact]
    public void TheTodayButtonRestsOnTheCurrentMonth()
    {
        var strip = Strip(October);

        Assert.True(strip.Find(".pspad-month-today").HasAttribute("disabled"));
    }

    [Fact]
    public void AnOlderMonthAnchorsTheStripSoItStaysVisibleAtTheOldEnd()
    {
        var strip = Strip(new YearMonth(2024, 6));

        Assert.Equal(12, strip.FindAll(".pspad-month-chip").Count);
        Assert.Equal("May 2025", strip.FindAll(".pspad-month-chip")[0].GetAttribute("aria-label"));
        Assert.Equal("June 2024", strip.FindAll(".pspad-month-chip")[^1].GetAttribute("aria-label"));
        Assert.Equal(["June 2024"], Labels(strip, ".pspad-month-chip[aria-pressed='true']"));
        Assert.Equal(["2025", "2024"], strip.FindAll(".pspad-month-chip-year").Select(year => year.TextContent.Trim()));
        Assert.Empty(strip.FindAll(".pspad-month-chip-current"));
    }

    [Fact]
    public void AMonthJustOutsideTheWindowShiftsItByOnlyWhatItNeeds()
    {
        var strip = Strip(new YearMonth(2025, 10));

        Assert.Equal("September 2026", strip.FindAll(".pspad-month-chip")[0].GetAttribute("aria-label"));
        Assert.Equal("October 2025", strip.FindAll(".pspad-month-chip")[^1].GetAttribute("aria-label"));
    }

    [Fact]
    public void AFutureMonthLeadsTheStrip()
    {
        var strip = Strip(new YearMonth(2027, 1));

        Assert.Equal(12, strip.FindAll(".pspad-month-chip").Count);
        Assert.Equal("January 2027", strip.FindAll(".pspad-month-chip")[0].GetAttribute("aria-label"));
        Assert.Equal(["January 2027"], Labels(strip, ".pspad-month-chip[aria-pressed='true']"));
        Assert.Equal(["October 2026"], Labels(strip, ".pspad-month-chip-current"));
    }

    [Fact]
    public void TheCalendarButtonOpensASheetOnTheShownYear()
    {
        var strip = Strip(new YearMonth(2025, 3));

        strip.Find(".pspad-month-more").Click();

        strip.WaitForAssertion(() => Assert.Equal("2025", Text(strip, ".pspad-month-sheet-year")));
        Assert.Equal(
            ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"],
            strip.FindAll(".pspad-month-sheet-month").Select(month => month.TextContent.Trim()));
        Assert.Equal("Mar", Text(strip, ".pspad-month-sheet-month[aria-current]"));
    }

    [Fact]
    public void PickingAMonthInAnotherYearChangesTheMonthAndClosesTheSheet()
    {
        var strip = Strip(October);
        strip.Find(".pspad-month-more").Click();
        strip.WaitForAssertion(() => strip.Find(".pspad-month-sheet-year"));

        strip.Find(".pspad-month-sheet-previous").Click();
        strip.FindAll(".pspad-month-sheet-month").Single(month => month.TextContent.Trim() == "Jan").Click();

        Assert.Equal(new YearMonth(2025, 1), _picked);
        strip.WaitForAssertion(() => Assert.Empty(strip.FindAll(".pspad-month-sheet-year")));
    }

    [Fact]
    public void ThisMonthInTheSheetJumpsBackToTheCurrentMonth()
    {
        var strip = Strip(new YearMonth(2024, 6));
        strip.Find(".pspad-month-more").Click();
        strip.WaitForAssertion(() => strip.Find(".pspad-month-sheet-year"));

        strip.Find(".pspad-month-sheet-current").Click();

        Assert.Equal(October, _picked);
    }
}
