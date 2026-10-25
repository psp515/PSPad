using System.Globalization;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;
using static PSPad.Module.Tasks.Tests.Lists.View.ViewTasks;

namespace PSPad.Module.Tasks.Tests.Lists.View;

[UnitTest]
public class ListSortTests
{
    static readonly StringComparer Names = StringComparer.OrdinalIgnoreCase;

    static string[] Sorted(ListSort sort, params TodoTask[] tasks) =>
        [.. sort.Apply(tasks, TodayDate, Names).Select(task => task.Name)];

    [Fact]
    public void DateIsTheDefaultEarliestFirstUndatedLast() =>
        Assert.Equal(["a", "b", "none"],
            Sorted(ListSort.Default, Undated("none"), Due(TodayDate.AddDays(5), "b"), Due(TodayDate.AddDays(-1), "a")));

    [Fact]
    public void LatestFirstStillPutsUndatedLast() =>
        Assert.Equal(["b", "a", "none"],
            Sorted(new ListSort(ListOrder.Date, true), Undated("none"), Due(TodayDate.AddDays(5), "b"), Due(TodayDate.AddDays(-1), "a")));

    [Fact]
    public void SameDateBreaksByPriorityThenName() =>
        Assert.Equal(["high", "a", "b"],
            Sorted(ListSort.Default, Due(TodayDate, "b"), Due(TodayDate, "a"), Prioritised(Due(TodayDate, "high"), Priority.High)));

    [Fact]
    public void AStepDeadlineCountsAsTheDate() =>
        Assert.Equal(["stepped", "due"],
            Sorted(ListSort.Default, Due(TodayDate.AddDays(3), "due"), WithStep(Due(TodayDate.AddDays(9), "stepped"), TodayDate.AddDays(1))));

    [Fact]
    public void PriorityHighFirstThenDate() =>
        Assert.Equal(["high", "medium-soon", "medium-late", "none"],
            Sorted(new ListSort(ListOrder.Priority, false),
                Undated("none"),
                Prioritised(Due(TodayDate.AddDays(4), "medium-late"), Priority.Medium),
                Prioritised(Due(TodayDate.AddDays(1), "medium-soon"), Priority.Medium),
                Prioritised(Undated("high"), Priority.High)));

    [Fact]
    public void PriorityLowFirstReversesOnlyThePriority() =>
        Assert.Equal(["none", "medium-soon", "medium-late", "high"],
            Sorted(new ListSort(ListOrder.Priority, true),
                Undated("none"),
                Prioritised(Due(TodayDate.AddDays(4), "medium-late"), Priority.Medium),
                Prioritised(Due(TodayDate.AddDays(1), "medium-soon"), Priority.Medium),
                Prioritised(Undated("high"), Priority.High)));

    [Fact]
    public void NameUsesTheComparerItIsGiven()
    {
        var polish = StringComparer.Create(CultureInfo.GetCultureInfo("pl-PL"), ignoreCase: true);
        var sorted = new ListSort(ListOrder.Name, false).Apply([Named("Żaba"), Named("Łódź"), Named("Lampa")], TodayDate, polish);

        Assert.Equal(["Lampa", "Łódź", "Żaba"], sorted.Select(task => task.Name));
    }

    [Fact]
    public void NameZToA() =>
        Assert.Equal(["c", "b", "a"], Sorted(new ListSort(ListOrder.Name, true), Named("a"), Named("c"), Named("b")));

    [Fact]
    public void AddedNewestFirstByDefault() =>
        Assert.Equal(["new", "old"],
            Sorted(new ListSort(ListOrder.Added, false), Named("old", Now.AddDays(-5)), Named("new", Now)));

    [Fact]
    public void AddedOldestFirstWhenDescending() =>
        Assert.Equal(["old", "new"],
            Sorted(new ListSort(ListOrder.Added, true), Named("old", Now.AddDays(-5)), Named("new", Now)));

    [Fact]
    public void EqualKeysKeepAStableIdOrder()
    {
        var first = Named("same");
        var second = Named("same");
        var expected = new[] { first, second }.OrderBy(task => task.Id).Select(task => task.Id);

        Assert.Equal(expected, new ListSort(ListOrder.Name, false).Apply([second, first], TodayDate, Names).Select(task => task.Id));
    }
}
