using Bunit;
using PSPad.App.Statistics;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class ConsistencyHeatmapTests : Bunit.TestContext
{
    [Fact]
    public void EveryDayInTheRangeGetsACell()
    {
        var days = Enumerable.Range(0, 10)
            .Select(offset => new DailyCountView(new DateOnly(2026, 9, 1).AddDays(offset), offset))
            .ToList();

        var heatmap = Render(days, new DateOnly(2026, 9, 10));

        Assert.Equal(10, heatmap.FindAll(".pspad-heatmap-cell").Count);
    }

    [Fact]
    public void AYearOfDaysStillGetsOneCellPerDay()
    {
        var days = Enumerable.Range(0, 365)
            .Select(offset => new DailyCountView(new DateOnly(2025, 9, 25).AddDays(offset), 1))
            .ToList();

        var heatmap = Render(days, new DateOnly(2026, 9, 24));

        Assert.Equal(365, heatmap.FindAll(".pspad-heatmap-cell").Count);
    }

    [Fact]
    public void ADayWithNoCompletionsGetsTheEmptyShade()
    {
        var days = new List<DailyCountView>
        {
            new(new DateOnly(2026, 9, 1), 0),
            new(new DateOnly(2026, 9, 2), 5)
        };

        var heatmap = Render(days, new DateOnly(2026, 9, 2));

        var empty = heatmap.FindAll(".pspad-heatmap-cell")[0];
        Assert.Equal("0", empty.GetAttribute("data-level"));
    }

    [Fact]
    public void TheBusiestDayGetsTheStrongestShade()
    {
        var days = new List<DailyCountView>
        {
            new(new DateOnly(2026, 9, 1), 1),
            new(new DateOnly(2026, 9, 2), 2),
            new(new DateOnly(2026, 9, 3), 9)
        };

        var heatmap = Render(days, new DateOnly(2026, 9, 3));

        var busiest = heatmap.FindAll(".pspad-heatmap-cell")[2];
        Assert.Equal("4", busiest.GetAttribute("data-level"));
    }

    [Fact]
    public void EachCellCarriesItsDateAndCountForScreenReaders()
    {
        var days = new List<DailyCountView> { new(new DateOnly(2026, 9, 24), 3) };

        var heatmap = Render(days, new DateOnly(2026, 9, 24));

        var cell = heatmap.FindAll(".pspad-heatmap-cell")[0];
        Assert.Equal("2026-09-24: 3 completed", cell.GetAttribute("aria-label"));
        Assert.Equal("2026-09-24: 3 completed", cell.GetAttribute("title"));
    }

    [Fact]
    public void ACellStillCarriesItsFullAccessibleLabelAlongsideTheVisibleDayNumber()
    {
        var days = new List<DailyCountView> { new(new DateOnly(2026, 9, 6), 2) };

        var heatmap = Render(days, new DateOnly(2026, 9, 6));

        var cell = heatmap.FindAll(".pspad-heatmap-cell")[0];
        Assert.Equal("2026-09-06: 2 completed", cell.GetAttribute("aria-label"));
        Assert.Equal("6", cell.QuerySelector(".pspad-heatmap-day-number")!.TextContent.Trim());
    }

    [Fact]
    public void ARangeWhereEveryDayIsZeroRendersWithoutDividingByZero()
    {
        var days = Enumerable.Range(0, 5)
            .Select(offset => new DailyCountView(new DateOnly(2026, 9, 1).AddDays(offset), 0))
            .ToList();

        var heatmap = Render(days, new DateOnly(2026, 9, 5));

        Assert.All(
            heatmap.FindAll(".pspad-heatmap-cell"),
            cell => Assert.Equal("0", cell.GetAttribute("data-level")));
    }

    [Fact]
    public void EachCellShowsItsDayOfMonthNumber()
    {
        var days = new List<DailyCountView>
        {
            new(new DateOnly(2026, 9, 1), 0),
            new(new DateOnly(2026, 9, 24), 0)
        };

        var heatmap = Render(days, new DateOnly(2026, 9, 24));

        var numbers = heatmap.FindAll(".pspad-heatmap-day-number")
            .Select(number => number.TextContent.Trim())
            .ToList();

        Assert.Equal(["1", "24"], numbers);
    }

    [Fact]
    public void TheGridIsAFlowingAutoFitGridSoItFillsThePanelWidth()
    {
        var days = Enumerable.Range(0, 30)
            .Select(offset => new DailyCountView(new DateOnly(2026, 8, 26).AddDays(offset), 1))
            .ToList();

        var heatmap = Render(days, new DateOnly(2026, 9, 24));

        var flow = heatmap.Find(".pspad-heatmap-flow");
        Assert.Contains("--pspad-heatmap-cell-min", flow.GetAttribute("style"));
        Assert.Contains("--pspad-heatmap-cell-cap", flow.GetAttribute("style"));
    }

    [Fact]
    public void SundaysAreMarkedAndOtherDaysAreNot()
    {
        var days = new List<DailyCountView>
        {
            new(new DateOnly(2026, 9, 6), 1),
            new(new DateOnly(2026, 9, 7), 1)
        };

        var heatmap = Render(days, new DateOnly(2026, 9, 7));

        var cells = heatmap.FindAll(".pspad-heatmap-cell");
        Assert.Equal("true", cells[0].GetAttribute("data-sunday"));
        Assert.Equal("false", cells[1].GetAttribute("data-sunday"));
    }

    [Fact]
    public void AShadeKeyExplainsWhatTheColoursMean()
    {
        var days = new List<DailyCountView> { new(new DateOnly(2026, 9, 24), 1) };

        var heatmap = Render(days, new DateOnly(2026, 9, 24));

        var levels = heatmap.FindAll(".pspad-heatmap-key-cell")
            .Select(cell => cell.GetAttribute("data-level"))
            .ToList();

        Assert.Equal(["0", "1", "2", "3", "4"], levels);
        Assert.Contains("Less", heatmap.Markup);
        Assert.Contains("More", heatmap.Markup);
        Assert.Contains("darker means more tasks finished", heatmap.Markup);
    }

    [Fact]
    public void TheKeysSwatchesAreNotCountedAsDays()
    {
        var days = new List<DailyCountView> { new(new DateOnly(2026, 9, 24), 1) };

        var heatmap = Render(days, new DateOnly(2026, 9, 24));

        Assert.Single(heatmap.FindAll(".pspad-heatmap-cell"));
    }

    IRenderedComponent<ConsistencyHeatmap> Render(IReadOnlyList<DailyCountView> days, DateOnly today) =>
        Render<ConsistencyHeatmap>(parameters => parameters
            .Add(p => p.Days, days)
            .Add(p => p.Today, today));
}
