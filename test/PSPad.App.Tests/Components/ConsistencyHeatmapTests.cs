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
    public void TheGridDeclaresOneColumnPerWeekSoItCanStretchToThePanel()
    {
        var days = Enumerable.Range(0, 30)
            .Select(offset => new DailyCountView(new DateOnly(2026, 8, 26).AddDays(offset), 1))
            .ToList();

        var heatmap = Render(days, new DateOnly(2026, 9, 24));

        Assert.Contains(
            "--pspad-heatmap-columns:5",
            heatmap.Find(".pspad-heatmap-grid").GetAttribute("style"));
    }

    [Fact]
    public void AYearOfDaysDeclaresAColumnForEveryWeekOfIt()
    {
        var days = Enumerable.Range(0, 365)
            .Select(offset => new DailyCountView(new DateOnly(2025, 9, 25).AddDays(offset), 1))
            .ToList();

        var heatmap = Render(days, new DateOnly(2026, 9, 24));

        Assert.Contains(
            "--pspad-heatmap-columns:53",
            heatmap.Find(".pspad-heatmap-grid").GetAttribute("style"));
    }

    [Fact]
    public void TheMonthsTheRangeCoversAreLabelledAlongTheTop()
    {
        var days = Enumerable.Range(0, 90)
            .Select(offset => new DailyCountView(new DateOnly(2026, 6, 27).AddDays(offset), 1))
            .ToList();

        var heatmap = Render(days, new DateOnly(2026, 9, 24));

        var months = heatmap.FindAll(".pspad-heatmap-month")
            .Select(label => label.TextContent.Trim())
            .ToList();

        Assert.Equal(["Jul", "Aug", "Sep"], months);
    }

    [Fact]
    public void AMonthTheRangeBarelyTouchesIsLeftUnlabelledSoTheLabelsDoNotCollide()
    {
        var days = Enumerable.Range(0, 30)
            .Select(offset => new DailyCountView(new DateOnly(2026, 8, 26).AddDays(offset), 1))
            .ToList();

        var heatmap = Render(days, new DateOnly(2026, 9, 24));

        var months = heatmap.FindAll(".pspad-heatmap-month")
            .Select(label => label.TextContent.Trim())
            .ToList();

        Assert.Equal(["Sep"], months);
    }

    [Fact]
    public void ARangeInsideOneMonthStillNamesIt()
    {
        var days = Enumerable.Range(0, 5)
            .Select(offset => new DailyCountView(new DateOnly(2026, 9, 7).AddDays(offset), 1))
            .ToList();

        var heatmap = Render(days, new DateOnly(2026, 9, 11));

        Assert.Equal("Sep", Assert.Single(heatmap.FindAll(".pspad-heatmap-month")).TextContent.Trim());
    }

    [Fact]
    public void EveryOtherWeekdayIsLabelledDownTheSide()
    {
        var days = new List<DailyCountView> { new(new DateOnly(2026, 9, 24), 1) };

        var heatmap = Render(days, new DateOnly(2026, 9, 24));

        var weekdays = heatmap.FindAll(".pspad-heatmap-weekday")
            .Select(label => label.TextContent.Trim())
            .ToList();

        Assert.Equal(["Mon", "Wed", "Fri"], weekdays);
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
