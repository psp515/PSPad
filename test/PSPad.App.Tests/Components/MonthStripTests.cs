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

    static IReadOnlyList<AngleSharp.Dom.IElement> Chips(IRenderedComponent<ContainerFragment> strip) =>
        strip.FindAll("[role=group] button");

    static string[] Labels(IEnumerable<AngleSharp.Dom.IElement> chips) =>
        [.. chips.Select(chip => chip.GetAttribute("aria-label")!)];

    static string Squash(string text) => string.Concat(text.Where(character => !char.IsWhiteSpace(character)));

    static void Resize(IRenderedComponent<ContainerFragment> strip, double width) =>
        strip.InvokeAsync(() => strip.FindComponent<MonthStrip>().Instance.Resized(width)).GetAwaiter().GetResult();

    static AngleSharp.Dom.IElement Sheet(IRenderedComponent<ContainerFragment> strip) => strip.Find("[role=dialog]");

    static IReadOnlyList<AngleSharp.Dom.IElement> SheetMonths(IRenderedComponent<ContainerFragment> strip) =>
        [.. Sheet(strip).QuerySelectorAll("button").Where(button => button.GetAttribute("aria-label") is null)
            .Where(button => button.TextContent.Trim() != "This month" && button.TextContent.Trim() != "Cancel")];

    [Fact]
    public void BeforeItIsMeasuredTheStripListsTwelveMonthsNewestFirst()
    {
        var strip = Strip(October);

        Assert.Equal(
        [
            "October 2026", "September 2026", "August 2026", "July 2026", "June 2026", "May 2026", "April 2026", "March 2026",
            "February 2026", "January 2026", "December 2025", "November 2025"
        ], Labels(Chips(strip)));
        Assert.Equal(["Oct", "Sep", "Aug", "Jul", "Jun", "May", "Apr", "Mar", "Feb", "Jan2026", "Dec", "Nov2025"],
            Chips(strip).Select(chip => Squash(chip.TextContent)));
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

        Assert.Equal(chips, Chips(strip).Count);
        Assert.Equal("October 2026", Chips(strip)[0].GetAttribute("aria-label"));
    }

    [Fact]
    public void TheShownMonthIsPressedAndTheCurrentMonthIsMarked()
    {
        var strip = Strip(new YearMonth(2026, 9));

        Assert.Equal(["September 2026"], Labels(Chips(strip).Where(chip => chip.GetAttribute("aria-pressed") == "true")));
        Assert.Equal(["October 2026"], Labels(Chips(strip).Where(chip => chip.GetAttribute("aria-current") == "date")));
    }

    [Fact]
    public void MonthsWithDataCarryADot()
    {
        var strip = Strip(October, new YearMonth(2026, 8), new YearMonth(2025, 12));

        Assert.Equal(["August 2026", "December 2025"],
            Labels(Chips(strip).Where(chip => chip.GetAttribute("data-testid") == "month-chip-marked")));
    }

    [Fact]
    public void TappingAChipShowsThatMonth()
    {
        var strip = Strip(October);

        strip.Find("button[aria-label='August 2026']").Click();

        Assert.Equal(new YearMonth(2026, 8), _picked);
    }

    [Fact]
    public void TheTodayButtonJumpsToTheCurrentMonth()
    {
        var strip = Strip(new YearMonth(2026, 3));
        Assert.False(strip.Find("button[aria-label='This month']").HasAttribute("disabled"));

        strip.Find("button[aria-label='This month']").Click();

        Assert.Equal(October, _picked);
    }

    [Fact]
    public void TheTodayButtonRestsOnTheCurrentMonth()
    {
        var strip = Strip(October);

        Assert.True(strip.Find("button[aria-label='This month']").HasAttribute("disabled"));
    }

    [Fact]
    public void AnOlderMonthAnchorsTheStripSoItStaysVisibleAtTheOldEnd()
    {
        var strip = Strip(new YearMonth(2024, 6));

        var chips = Chips(strip);
        Assert.Equal(12, chips.Count);
        Assert.Equal("May 2025", chips[0].GetAttribute("aria-label"));
        Assert.Equal("June 2024", chips[^1].GetAttribute("aria-label"));
        Assert.Equal(["June 2024"], Labels(chips.Where(chip => chip.GetAttribute("aria-pressed") == "true")));
        Assert.DoesNotContain(chips, chip => chip.GetAttribute("aria-current") == "date");
        Assert.Equal(["Jan2025", "Jun2024"], chips.Select(chip => Squash(chip.TextContent)).Where(text => text.Any(char.IsDigit)));
    }

    [Fact]
    public void AMonthJustOutsideTheWindowShiftsItByOnlyWhatItNeeds()
    {
        var chips = Chips(Strip(new YearMonth(2025, 10)));

        Assert.Equal("September 2026", chips[0].GetAttribute("aria-label"));
        Assert.Equal("October 2025", chips[^1].GetAttribute("aria-label"));
    }

    [Fact]
    public void AFutureMonthLeadsTheStrip()
    {
        var chips = Chips(Strip(new YearMonth(2027, 1)));

        Assert.Equal(12, chips.Count);
        Assert.Equal("January 2027", chips[0].GetAttribute("aria-label"));
        Assert.Equal(["January 2027"], Labels(chips.Where(chip => chip.GetAttribute("aria-pressed") == "true")));
        Assert.Equal(["October 2026"], Labels(chips.Where(chip => chip.GetAttribute("aria-current") == "date")));
    }

    [Fact]
    public void TheCalendarButtonOpensASheetOnTheShownYear()
    {
        var strip = Strip(new YearMonth(2025, 3));

        strip.Find("button[aria-label='Pick another month']").Click();

        strip.WaitForAssertion(() => Assert.Contains("2025", Sheet(strip).TextContent));
        Assert.Equal(
            ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"],
            SheetMonths(strip).Select(month => month.TextContent.Trim()));
        Assert.Equal(["Mar"], SheetMonths(strip).Where(month => month.HasAttribute("aria-current")).Select(month => month.TextContent.Trim()));
    }

    [Fact]
    public void PickingAMonthInAnotherYearChangesTheMonthAndClosesTheSheet()
    {
        var strip = Strip(October);
        strip.Find("button[aria-label='Pick another month']").Click();
        strip.WaitForAssertion(() => Sheet(strip));

        Sheet(strip).QuerySelector("button[aria-label='Previous year']")!.Click();
        SheetMonths(strip).Single(month => month.TextContent.Trim() == "Jan").Click();

        Assert.Equal(new YearMonth(2025, 1), _picked);
        strip.WaitForAssertion(() => Assert.Empty(strip.FindAll("[role=dialog]")));
    }

    [Fact]
    public void ThisMonthInTheSheetJumpsBackToTheCurrentMonth()
    {
        var strip = Strip(new YearMonth(2024, 6));
        strip.Find("button[aria-label='Pick another month']").Click();
        strip.WaitForAssertion(() => Sheet(strip));

        Sheet(strip).QuerySelectorAll("button").Single(button => button.TextContent.Trim() == "This month").Click();

        Assert.Equal(October, _picked);
    }
}
