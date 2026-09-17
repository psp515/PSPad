using Bunit;
using PSPad.App.Pages;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class AppInfoPageTests : Bunit.TestContext
{
    [Fact]
    public void ItShowsTheVersion()
    {
        Arrange();

        var page = Render<AppInfoPage>();

        Assert.Contains("Version 0.1.0", page.Markup);
    }

    [Fact]
    public void ItShowsTheLicense()
    {
        Arrange();

        var page = Render<AppInfoPage>();

        Assert.Contains("GNU General Public License v3", page.Markup);
    }

    [Fact]
    public void ItLinksToGitHubAndDocs()
    {
        Arrange();

        var page = Render<AppInfoPage>();

        Assert.Contains("https://github.com/psp515/PSPad", page.Markup);
        Assert.Contains("https://psp515.github.io", page.Markup);
    }

    void Arrange() => AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));
}
