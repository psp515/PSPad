using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class AreaSummaryTests
{
    [Theory]
    [InlineData(0, 0, "0 lists · no open tasks")]
    [InlineData(1, 0, "1 list · no open tasks")]
    [InlineData(1, 1, "1 list · 1 open task")]
    [InlineData(2, 3, "2 lists · 3 open tasks")]
    public void TaskListsOnlyCountListsAndOpenTasks(int taskLists, int openTasks, string expected) =>
        Assert.Equal(expected, AreaSummary.Describe(taskLists, 0, openTasks, 0));

    [Theory]
    [InlineData(1, 0, "1 reference list · no items")]
    [InlineData(1, 1, "1 reference list · 1 item")]
    [InlineData(2, 7, "2 reference lists · 7 items")]
    public void ReferenceListsOnlyCountReferenceListsAndItems(int referenceLists, int items, string expected) =>
        Assert.Equal(expected, AreaSummary.Describe(0, referenceLists, 0, items));

    [Theory]
    [InlineData(1, 1, 1, 1, "2 lists · 1 open task · 1 item")]
    [InlineData(2, 1, 3, 5, "3 lists · 3 open tasks · 5 items")]
    [InlineData(1, 1, 0, 4, "2 lists · 4 items")]
    [InlineData(1, 1, 2, 0, "2 lists · 2 open tasks")]
    [InlineData(1, 1, 0, 0, "2 lists · no open tasks")]
    public void BothKindsCountAllListsAndLeaveOutAZeroPart(int taskLists, int referenceLists, int openTasks, int items, string expected) =>
        Assert.Equal(expected, AreaSummary.Describe(taskLists, referenceLists, openTasks, items));
}
