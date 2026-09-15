using MudBlazor;

namespace PSPad.App.State;

public sealed class BrowserViewport(IBrowserViewportService service) : IViewport
{
    readonly Guid _observerId = Guid.NewGuid();

    public Task SubscribeAsync(Action<bool> onDesktopChanged) =>
        service.SubscribeAsync(_observerId, async args =>
        {
            var isDesktop = await service.IsBreakpointWithinReferenceSizeAsync(Breakpoint.MdAndUp, args.Breakpoint);
            onDesktopChanged(isDesktop);
        }, options: null, fireImmediately: true);

    public ValueTask DisposeAsync() => new(service.UnsubscribeAsync(_observerId));
}
