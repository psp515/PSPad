using PSPad.Module.Tasks.Today;

namespace PSPad.App.State;

public sealed record TimelineBlock(TodayEntry Entry, int Start, int End, int Lane, int Lanes);
