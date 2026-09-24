namespace PSPad.Contracts;

public sealed record DailyCompletionsView(DateOnly Day, int Planned, int Unplanned);
