using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Components;
using PSPad.App.Sync;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class SidebarFooterTests : Bunit.TestContext
{
    [Fact]
    public void ItShowsTheClockAndTheLicense()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var footer = Render<SidebarFooter>();

        Assert.Contains("PSPad", footer.Markup);
        Assert.Contains("GPL v3", footer.Markup);
        Assert.Equal(2, footer.Find(".pspad-sidebar-footer").Children.Length);
    }

    [Fact]
    public void ItShowsTheConnectionStatusOnlyWhenSomethingIsWrong()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));
        var reachability = Services.GetRequiredService<ServerReachability>();

        reachability.Failed(browserIsOnline: true);
        var footer = Render<SidebarFooter>();

        Assert.NotEmpty(footer.FindAll(".pspad-connection-status"));
    }
}
