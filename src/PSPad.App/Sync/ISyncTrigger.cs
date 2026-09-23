namespace PSPad.App.Sync;

public interface ISyncTrigger
{
    Task SyncNowAsync();
}
