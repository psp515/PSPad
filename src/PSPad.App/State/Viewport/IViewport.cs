namespace PSPad.App.State.Viewport;

public interface IViewport : IAsyncDisposable
{
    Task SubscribeAsync(Action<bool> onDesktopChanged);
}
