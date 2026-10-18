using Microsoft.Extensions.Localization;
using PSPad.App.Resources;
using PSPad.Module.Tasks.Goals;

namespace PSPad.App.Components;

public static class GoalStatusText
{
    public static string Of(GoalStatus status, IStringLocalizer<GoalsStrings> localizer) => status switch
    {
        GoalStatus.Achieved => localizer["StatusAchieved"],
        GoalStatus.NotAchieved => localizer["StatusNotAchieved"],
        _ => localizer["StatusInProgress"]
    };
}
