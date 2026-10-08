using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Layout;
using PSPad.App.Sync;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class PullToRefreshTests : Bunit.TestContext
{
    readonly GatedSyncTrigger _trigger = new();
    readonly SwitchableConnectivity _connectivity = new();

    [Fact]
    public async Task APullRunsOneSync()
    {
        var pull = RenderPull();

        await pull.InvokeAsync(() => pull.Instance.OnPulled());

        Assert.Equal(1, _trigger.Calls);
    }

    [Fact]
    public async Task APullOfflineDoesNothing()
    {
        _connectivity.IsOnline = false;
        var pull = RenderPull();

        await pull.InvokeAsync(() => pull.Instance.OnPulled());

        Assert.Equal(0, _trigger.Calls);
    }

    [Fact]
    public async Task APullDuringASyncDoesNotStartAnother()
    {
        var pull = RenderPull();
        _trigger.Hold();

        var first = pull.InvokeAsync(() => pull.Instance.OnPulled());
        await pull.InvokeAsync(() => pull.Instance.OnPulled());
        _trigger.Release();
        await first;

        Assert.Equal(1, _trigger.Calls);
    }

    IRenderedComponent<PullToRefresh> RenderPull()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));
        Services.AddSingleton<ISyncTrigger>(_trigger);
        Services.AddSingleton<ISyncStatus>(_trigger);
        Services.AddSingleton<IConnectivity>(_connectivity);
        return Render<PullToRefresh>();
    }
}
