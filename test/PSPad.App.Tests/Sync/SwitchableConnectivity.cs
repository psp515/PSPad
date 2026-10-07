using PSPad.App.Sync;

namespace PSPad.App.Tests;

public sealed class SwitchableConnectivity : IConnectivity
{
    public bool IsOnline { get; set; } = true;

#pragma warning disable CS0067
    public event Action? CameOnline;
#pragma warning restore CS0067

    public event Action? Changed;

    public void GoOnline()
    {
        IsOnline = true;
        Changed?.Invoke();
    }
}
