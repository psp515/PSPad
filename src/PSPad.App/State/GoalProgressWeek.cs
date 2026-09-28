namespace PSPad.App.State;

public sealed record GoalProgressWeek(DateOnly WeekStart, int Total, int Done);
