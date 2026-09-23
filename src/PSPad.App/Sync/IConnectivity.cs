namespace PSPad.App.Sync;

public interface IConnectivity
{
    bool IsOnline { get; }

    event Action? CameOnline;

    event Action? Changed;
}
