using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.App.Layout;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class NavSidebarTests : Bunit.TestContext
{
    [Theory]
    [InlineData("Today")]
    [InlineData("Inbox")]
    [InlineData("Areas")]
    [InlineData("Goals")]
    [InlineData("History")]
    public void ItListsEverySection(string section)
    {
        Arrange();

        var sidebar = Render<NavSidebar>();

        Assert.Contains(section, sidebar.Markup);
    }

    [Fact]
    public void EverySectionLinksSomewhere()
    {
        Arrange();

        var sidebar = Render<NavSidebar>();

        Assert.Equal(5, sidebar.FindAll("a").Count);
    }

    void Arrange()
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        Services.AddMudServices();
    }
}
