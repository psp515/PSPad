namespace PSPad.App.Markdown;

public sealed class MarkdownViewClickEventArgs : EventArgs
{
    public bool OnLink { get; set; }

    public bool HasSelection { get; set; }
}
