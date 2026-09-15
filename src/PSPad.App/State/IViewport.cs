namespace PSPad.App.State;

public interface IViewport : IAsyncDisposable
{
    Task SubscribeAsync(Action<bool> onDesktopChanged);
}
