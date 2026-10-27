namespace PSPad.App.State;

public sealed record TimelineSegment(int StartHour, int EndHour, bool Folded, int Weight);
