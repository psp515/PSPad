using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class PageHeaderTests
{
    [Fact]
    public void SettingAHeaderAnnouncesIt()
    {
        var header = new PageHeader();
        var changes = 0;
        header.Changed += () => changes++;

        header.Set("Shopping", "Home", "/areas/1");

        Assert.Equal(1, changes);
        Assert.Equal("Shopping", header.Title);
        Assert.Equal("Home", header.Subtitle);
        Assert.Equal("/areas/1", header.BackHref);
    }

    [Fact]
    public void SettingTheSameHeaderAgainStaysQuiet()
    {
        var header = new PageHeader();
        header.Set("Inbox", null, null);
        var changes = 0;
        header.Changed += () => changes++;

        header.Set("Inbox", null, null);

        Assert.Equal(0, changes);
    }
}
