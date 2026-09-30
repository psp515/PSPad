using MudBlazor;

namespace PSPad.App.State.Viewport;

public sealed class BrowserBreakpoints(IBrowserViewportService service) : IBreakpoints
{
    readonly Guid _observerId = Guid.NewGuid();

    public Task SubscribeAsync(Action<Breakpoint> onChanged) =>
        service.SubscribeAsync(_observerId, args => onChanged(args.Breakpoint), options: null, fireImmediately: true);

    public ValueTask DisposeAsync() => new(service.UnsubscribeAsync(_observerId));
}
