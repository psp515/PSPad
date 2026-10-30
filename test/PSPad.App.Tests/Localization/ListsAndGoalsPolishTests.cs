using System.Globalization;
using PSPad.App.Components;
using PSPad.App.Localization;
using PSPad.App.Resources;
using PSPad.App.State;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Localization;

[UnitTest]
[Collection("Culture")]
public class ListsAndGoalsPolishTests
{
    [Theory]
    [InlineData(2, 0, 3, 0, "2 listy · 3 otwarte zadania")]
    [InlineData(1, 1, 1, 2, "2 listy · 1 otwarte zadanie · 2 pozycje")]
    [InlineData(0, 5, 0, 0, "5 list materiałów · brak pozycji")]
    public void TheAreaSummaryAgreesWithTheCountsInPolish(
        int taskLists, int referenceLists, int openTasks, int items, string expected) =>
        InPolish(() => Assert.Equal(expected, AreaSummary.Describe(
            taskLists, referenceLists, openTasks, items,
            TestLocalizers.For<ListsStrings>(), TestLocalizers.For<TasksStrings>())));

    [Fact]
    public void TheDeleteWarningNamesTheContentsInTheInstrumentalCase() =>
        InPolish(() =>
        {
            var areaId = Guid.NewGuid();
            var list = NewList(areaId);

            var message = DeleteWarning.ForArea("Dom", areaId, [list], [], [], TestLocalizers.For<ListsStrings>());

            Assert.Equal("Usunąć „Dom” razem z 1 listą? Tej operacji nie można cofnąć.", message);
        });

    [Theory]
    [InlineData(GoalStatus.InProgress, "W toku")]
    [InlineData(GoalStatus.Achieved, "Osiągnięty")]
    [InlineData(GoalStatus.NotAchieved, "Nieosiągnięty")]
    public void GoalStatusesSpeakPolish(GoalStatus status, string expected) =>
        InPolish(() => Assert.Equal(expected, GoalStatusText.Of(status, TestLocalizers.For<GoalsStrings>())));

    [Theory]
    [InlineData(1, "Został 1 dzień")]
    [InlineData(3, "Zostały 3 dni")]
    [InlineData(5, "Zostało 5 dni")]
    public void DaysLeftIsAWholeSentenceThatAgreesWithTheCount(int days, string expected) =>
        InPolish(() => Assert.Equal(expected, TestLocalizers.For<GoalsStrings>().Plural(days, "PaceDaysLeft")));

    [Theory]
    [InlineData(0, "Nikt jeszcze nie dołączył · link wygaśnie za 7 min")]
    [InlineData(1, "Dołączyła 1 osoba · link wygaśnie za 7 min")]
    [InlineData(2, "Dołączyły 2 osoby · link wygaśnie za 7 min")]
    [InlineData(6, "Dołączyło 6 osób · link wygaśnie za 7 min")]
    public void TheMembersSummaryWithALinkAgreesWithTheCount(int joined, string expected) =>
        InPolish(() => Assert.Equal(expected, TestLocalizers.For<ListsStrings>().Plural(joined, "JoinedWithLink", 7)));

    static TaskList NewList(Guid areaId)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null,
            new CreateTaskList(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), areaId, "Lista", ListKind.Tasks),
            DateTimeOffset.UnixEpoch));
        return list;
    }

    [Theory]
    [InlineData(0, "brak otwartych zadań")]
    [InlineData(22, "22 otwarte zadania")]
    public void TheOpenTaskCountAgreesWithTheCountInPolish(int open, string expected) =>
        InPolish(() => Assert.Equal(expected, OpenTaskCount.Describe(open, TestLocalizers.For<TasksStrings>())));

    static void InPolish(Action assertion)
    {
        var before = CultureInfo.CurrentUICulture;
        var beforeFormat = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            assertion();
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
            CultureInfo.CurrentCulture = beforeFormat;
        }
    }
}
