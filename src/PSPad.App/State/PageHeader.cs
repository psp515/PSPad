namespace PSPad.App.State;

public sealed class PageHeader
{
    public string Title { get; private set; } = "";

    public string? Subtitle { get; private set; }

    public string? BackHref { get; private set; }

    public string? BackLabel { get; private set; }

    public event Action? Changed;

    public void Set(string title, string? subtitle, string? backHref, string? backLabel = null)
    {
        if (title == Title && subtitle == Subtitle && backHref == BackHref && backLabel == BackLabel)
        {
            return;
        }

        Title = title;
        Subtitle = subtitle;
        BackHref = backHref;
        BackLabel = backLabel;
        Changed?.Invoke();
    }
}
