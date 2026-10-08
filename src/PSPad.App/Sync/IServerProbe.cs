namespace PSPad.App.Sync;

public interface IServerProbe
{
    Task<bool> ReachableAsync();
}
