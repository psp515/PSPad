using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class ActiveTabTests
{
    [Theory]
    [InlineData("", NavTab.MyDay)]
    [InlineData("?task=0b8c1c7e-0000-0000-0000-000000000001", NavTab.MyDay)]
    [InlineData("inbox", NavTab.Inbox)]
    [InlineData("inbox?inbox=new", NavTab.Inbox)]
    [InlineData("areas", NavTab.Areas)]
    [InlineData("areas/0b8c1c7e-0000-0000-0000-000000000001", NavTab.Areas)]
    [InlineData("lists/0b8c1c7e-0000-0000-0000-000000000001?task=x", NavTab.Areas)]
    [InlineData("goals", NavTab.Goals)]
    [InlineData("goals/0b8c1c7e-0000-0000-0000-000000000001", NavTab.Goals)]
    [InlineData("statistics", NavTab.Statistics)]
    [InlineData("statistics#feed", NavTab.Statistics)]
    [InlineData("settings", NavTab.None)]
    [InlineData("app-info", NavTab.None)]
    [InlineData("search?q=milk", NavTab.None)]
    public void EachScreenLightsItsTab(string path, NavTab expected)
    {
        Assert.Equal(expected, ActiveTab.For(path));
    }
}
