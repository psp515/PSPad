namespace PSPad.Contracts;

public sealed record WeeklyCountView(DateOnly WeekStart, int Count);
