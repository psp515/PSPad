namespace PSPad.App.State;

public sealed record GoalPaceView(int TimePercent, int WorkPercent, int DaysLeft)
{
    public bool Behind => WorkPercent < TimePercent;
}
