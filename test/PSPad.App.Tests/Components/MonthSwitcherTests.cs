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
public class MonthSwitcherTests : Bunit.TestContext
{
    static readonly YearMonth October = new(2026, 10);

    YearMonth? _picked;

    public MonthSwitcherTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    IRenderedComponent<ContainerFragment> Switcher(YearMonth month) => Render(builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<MonthSwitcher>(2);
        builder.AddAttribute(3, nameof(MonthSwitcher.Month), month);
        builder.AddAttribute(4, nameof(MonthSwitcher.Current), October);
        builder.AddAttribute(5, nameof(MonthSwitcher.OnMonthChanged),
            EventCallback.Factory.Create<YearMonth>(this, picked => _picked = picked));
        builder.CloseComponent();
    });

    static string Text(IRenderedComponent<ContainerFragment> switcher, string selector) => switcher.Find(selector).TextContent.Trim();

    [Fact]
    public void TheTitleOpensASheetOnTheShownYear()
    {
        var switcher = Switcher(new YearMonth(2025, 3));

        switcher.Find(".pspad-month-title").Click();

        switcher.WaitForAssertion(() => Assert.Equal("2025", Text(switcher, ".pspad-month-sheet-year")));
        Assert.Equal(
            ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"],
            switcher.FindAll(".pspad-month-sheet-month").Select(month => month.TextContent.Trim()));
        Assert.Equal("Mar", Text(switcher, ".pspad-month-sheet-month[aria-current]"));
    }

    [Fact]
    public void PickingAMonthInAnotherYearChangesTheMonthAndClosesTheSheet()
    {
        var switcher = Switcher(October);
        switcher.Find(".pspad-month-title").Click();
        switcher.WaitForAssertion(() => switcher.Find(".pspad-month-sheet-year"));

        switcher.Find(".pspad-month-sheet-previous").Click();
        switcher.FindAll(".pspad-month-sheet-month").Single(month => month.TextContent.Trim() == "Jan").Click();

        Assert.Equal(new YearMonth(2025, 1), _picked);
        switcher.WaitForAssertion(() => Assert.Empty(switcher.FindAll(".pspad-month-sheet-year")));
    }

    [Fact]
    public void ThisMonthJumpsBackToTheCurrentMonth()
    {
        var switcher = Switcher(new YearMonth(2024, 6));
        switcher.Find(".pspad-month-title").Click();
        switcher.WaitForAssertion(() => switcher.Find(".pspad-month-sheet-year"));

        switcher.Find(".pspad-month-sheet-current").Click();

        Assert.Equal(October, _picked);
    }

    [Fact]
    public void TheArrowsStillStepOneMonth()
    {
        var switcher = Switcher(October);

        switcher.Find(".pspad-month-previous").Click();
        Assert.Equal(new YearMonth(2026, 9), _picked);

        switcher.Find(".pspad-month-next").Click();
        Assert.Equal(new YearMonth(2026, 11), _picked);
    }
}
