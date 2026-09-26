namespace PSPad.App.Updates;

public interface IAppUpdates
{
    bool IsAvailable { get; }

    event Action? Available;

    Task ApplyAsync();
}
