using PSPad.App.Live;

namespace PSPad.App.Tests.Live;

public sealed class SwitchableVisibility : IPageVisibility
{
    public bool IsVisible { get; private set; } = true;

    public event Action? Changed;

    public void Hide()
    {
        IsVisible = false;
        Changed?.Invoke();
    }

    public void Show()
    {
        IsVisible = true;
        Changed?.Invoke();
    }
}
