using PSPad.Module.Tasks.Today;

namespace PSPad.App.State;

public sealed record TimelineGroup(IReadOnlyList<TodayEntry> Entries, int Start, int End, int Lane, int Lanes)
{
    const int LineHeight = 24;
    const int GroupPadding = 4;
    const int GroupBorder = 3;

    public int Needs => Entries.Count * LineHeight + (Entries.Count - 1) + GroupPadding + GroupBorder;
}
