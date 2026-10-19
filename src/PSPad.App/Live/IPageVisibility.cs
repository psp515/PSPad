namespace PSPad.App.Live;

public interface IPageVisibility
{
    bool IsVisible { get; }

    event Action? Changed;
}
