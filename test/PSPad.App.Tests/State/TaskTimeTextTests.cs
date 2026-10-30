using PSPad.App.State;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class TaskTimeTextTests
{
    [Theory]
    [InlineData(9, 30, 11, 0, "09:30–11:00")]
    [InlineData(9, 30, -1, 0, "09:30")]
    [InlineData(22, 0, 1, 0, "22:00–01:00 (+1)")]
    [InlineData(9, 5, 22, 0, "09:05–22:00")]
    public void ItDescribesATimeAsHoursAndMinutes(int sh, int sm, int eh, int em, string expected) =>
        Assert.Equal(expected, TaskTimeText.Describe(new TaskTime(new TimeOnly(sh, sm), eh < 0 ? null : new TimeOnly(eh, em))));
}
