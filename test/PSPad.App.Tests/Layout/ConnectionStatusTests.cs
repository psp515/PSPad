using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Components;
using PSPad.App.Sync;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class ConnectionStatusTests : Bunit.TestContext
{
    readonly SwitchableConnectivity _connectivity = new();

    [Fact]
    public void ItDrawsNothingWhileEverythingWorks()
    {
        var status = RenderStatus();

        Assert.Empty(status.FindAll(".pspad-connection-status"));
    }

    [Fact]
    public void OfflineIsAnIconWithALabelAndNoText()
    {
        _connectivity.IsOnline = false;

        var status = RenderStatus();

        var icon = status.Find(".pspad-connection-status");
        Assert.Equal("Offline", icon.GetAttribute("aria-label"));
        Assert.DoesNotContain("Offline", status.Markup.Replace("aria-label=\"Offline\"", ""));
    }

    [Fact]
    public void AnUnreachableServerIsAnIconWithALabelAndNoText()
    {
        var status = RenderStatus();
        Services.GetRequiredService<ServerReachability>().Failed(browserIsOnline: true);

        status.WaitForAssertion(() =>
            Assert.Equal("Server unreachable", status.Find(".pspad-connection-status").GetAttribute("aria-label")));
        Assert.DoesNotContain("Server unreachable", status.Markup.Replace("aria-label=\"Server unreachable\"", ""));
    }

    IRenderedComponent<ConnectionStatus> RenderStatus()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));
        Services.AddSingleton<IConnectivity>(_connectivity);
        return Render<ConnectionStatus>();
    }
}
