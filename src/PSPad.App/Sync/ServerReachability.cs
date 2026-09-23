namespace PSPad.App.Sync;

public sealed class ServerReachability
{
    public bool IsReachable { get; private set; } = true;

    public event Action? Changed;

    public void Failed(bool browserIsOnline)
    {
        // A failure with no network is the offline case the app is built for, not a server fault.
        if (!browserIsOnline || !IsReachable)
        {
            return;
        }

        IsReachable = false;
        Changed?.Invoke();
    }

    public void Succeeded()
    {
        if (IsReachable)
        {
            return;
        }

        IsReachable = true;
        Changed?.Invoke();
    }
}
