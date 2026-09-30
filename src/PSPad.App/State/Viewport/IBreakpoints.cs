using MudBlazor;

namespace PSPad.App.State.Viewport;

public interface IBreakpoints : IAsyncDisposable
{
    Task SubscribeAsync(Action<Breakpoint> onChanged);
}
