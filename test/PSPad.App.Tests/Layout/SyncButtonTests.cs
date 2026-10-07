using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Layout;
using PSPad.App.Sync;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class SyncButtonTests : Bunit.TestContext
{
    readonly GatedSyncTrigger _trigger = new();
    readonly SwitchableConnectivity _connectivity = new();

    [Fact]
    public void ClickingRunsOneSync()
    {
        var button = RenderButton();

        button.Find(".pspad-sync-button").Click();

        Assert.Equal(1, _trigger.Calls);
    }

    [Fact]
    public void ItIsDisabledWhileASyncRuns()
    {
        var button = RenderButton();
        _trigger.Hold();

        button.Find(".pspad-sync-button").Click();

        button.WaitForAssertion(() => Assert.True(button.Find(".pspad-sync-button").HasAttribute("disabled")));
        _trigger.Release();
        button.WaitForAssertion(() => Assert.False(button.Find(".pspad-sync-button").HasAttribute("disabled")));
    }

    [Fact]
    public void ItIsDisabledOffline()
    {
        _connectivity.IsOnline = false;

        var button = RenderButton();

        Assert.True(button.Find(".pspad-sync-button").HasAttribute("disabled"));
    }

    [Fact]
    public void ItFollowsConnectivityChanges()
    {
        _connectivity.IsOnline = false;
        var button = RenderButton();

        _connectivity.GoOnline();

        button.WaitForAssertion(() => Assert.False(button.Find(".pspad-sync-button").HasAttribute("disabled")));
    }

    [Fact]
    public void TheLabelledFormSaysSyncNowAndRunsOneSync()
    {
        var button = RenderButton(labeled: true);

        Assert.Contains("Sync now", button.Find(".pspad-sync-button").TextContent);
        button.Find(".pspad-sync-button").Click();

        Assert.Equal(1, _trigger.Calls);
    }

    IRenderedComponent<SyncButton> RenderButton(bool labeled = false)
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));
        Services.AddSingleton<ISyncTrigger>(_trigger);
        Services.AddSingleton<ISyncStatus>(_trigger);
        Services.AddSingleton<IConnectivity>(_connectivity);
        return Render<SyncButton>(parameters => parameters.Add(p => p.Labeled, labeled));
    }
}
