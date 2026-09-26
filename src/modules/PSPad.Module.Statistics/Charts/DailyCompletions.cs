namespace PSPad.Module.Statistics;

public sealed record DailyCompletions(DateOnly Day, int Planned, int Unplanned);
