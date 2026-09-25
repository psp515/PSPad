using PSPad.Module.Tasks.Goals;

namespace PSPad.App.Components;

public static class GoalStatusText
{
    public static string Of(GoalStatus status) => status switch
    {
        GoalStatus.Achieved => "Achieved",
        GoalStatus.NotAchieved => "Not achieved",
        _ => "In progress"
    };
}
