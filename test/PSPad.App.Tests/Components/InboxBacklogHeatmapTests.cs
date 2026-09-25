using Bunit;
using PSPad.App.Statistics;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class InboxBacklogHeatmapTests : Bunit.TestContext
{
    static readonly DateOnly Today = new(2026, 9, 24);

    [Fact]
    public void EveryWeekInTheRangeGetsACell()
    {
        var weeks = Enumerable.Range(0, 5)
            .Select(offset => new WeeklyCountView(new DateOnly(2026, 8, 23).AddDays(offset * 7), offset))
            .ToList();

        var heatmap = Render(weeks);

        Assert.Equal(5, heatmap.FindAll(".pspad-heatmap-week").Count);
    }

    [Fact]
    public void EachCellSaysHowManyItemsThatWeekLeftBehind()
    {
        var weeks = new List<WeeklyCountView> { new(new DateOnly(2026, 9, 21), 4) };

        var heatmap = Render(weeks);

        var cell = heatmap.FindAll(".pspad-heatmap-week")[0];
        Assert.Equal("week of 2026-09-21: 4 still in the Inbox", cell.GetAttribute("aria-label"));
        Assert.Equal("week of 2026-09-21: 4 still in the Inbox", cell.GetAttribute("title"));
    }

    [Fact]
    public void AnEmptyWeekGetsTheEmptyShadeAndTheFullestWeekTheStrongest()
    {
        var weeks = new List<WeeklyCountView>
        {
            new(new DateOnly(2026, 9, 6), 0),
            new(new DateOnly(2026, 9, 13), 2),
            new(new DateOnly(2026, 9, 20), 9)
        };

        var heatmap = Render(weeks);

        var cells = heatmap.FindAll(".pspad-heatmap-week");
        Assert.Equal("0", cells[0].GetAttribute("data-level"));
        Assert.Equal("1", cells[1].GetAttribute("data-level"));
        Assert.Equal("4", cells[2].GetAttribute("data-level"));
    }

    [Fact]
    public void TheWeekTodayFallsInIsMarkedAsCurrent()
    {
        var weeks = new List<WeeklyCountView>
        {
            new(new DateOnly(2026, 9, 13), 1),
            new(new DateOnly(2026, 9, 20), 1)
        };

        var heatmap = Render(weeks);

        var cells = heatmap.FindAll(".pspad-heatmap-week");
        Assert.Equal("false", cells[0].GetAttribute("data-today"));
        Assert.Equal("true", cells[1].GetAttribute("data-today"));
    }

    [Fact]
    public void TheGridIsAFlowingAutoFitGridSoItFillsThePanelWidth()
    {
        var weeks = Enumerable.Range(0, 5)
            .Select(offset => new WeeklyCountView(new DateOnly(2026, 8, 23).AddDays(offset * 7), 1))
            .ToList();

        var heatmap = Render(weeks);

        var flow = heatmap.Find(".pspad-heatmap-flow");
        Assert.Contains("--pspad-heatmap-cell-min", flow.GetAttribute("style"));
        Assert.Contains("--pspad-heatmap-cell-cap", flow.GetAttribute("style"));
    }

    [Fact]
    public void AYearOfWeeksStillGetsOneCellPerWeek()
    {
        var weeks = Enumerable.Range(0, 53)
            .Select(offset => new WeeklyCountView(new DateOnly(2025, 9, 21).AddDays(offset * 7), 1))
            .ToList();

        var heatmap = Render(weeks);

        Assert.Equal(53, heatmap.FindAll(".pspad-heatmap-week").Count);
    }

    [Fact]
    public void AShadeKeySaysWhatTheColoursMeasure()
    {
        var weeks = new List<WeeklyCountView> { new(new DateOnly(2026, 9, 20), 1) };

        var heatmap = Render(weeks);

        var levels = heatmap.FindAll(".pspad-heatmap-key-cell")
            .Select(cell => cell.GetAttribute("data-level"))
            .ToList();

        Assert.Equal(["0", "1", "2", "3", "4"], levels);
        Assert.Contains("still waiting in the Inbox when that week ended", heatmap.Markup);
    }

    [Fact]
    public void ARangeWhereEveryWeekIsEmptyRendersWithoutDividingByZero()
    {
        var weeks = Enumerable.Range(0, 4)
            .Select(offset => new WeeklyCountView(new DateOnly(2026, 8, 30).AddDays(offset * 7), 0))
            .ToList();

        var heatmap = Render(weeks);

        Assert.All(
            heatmap.FindAll(".pspad-heatmap-week"),
            cell => Assert.Equal("0", cell.GetAttribute("data-level")));
    }

    IRenderedComponent<InboxBacklogHeatmap> Render(IReadOnlyList<WeeklyCountView> weeks) =>
        Render<InboxBacklogHeatmap>(parameters => parameters
            .Add(p => p.Weeks, weeks)
            .Add(p => p.Today, Today));
}
