using System.Text.RegularExpressions;

namespace PSPad.Module.Statistics;

public static partial class HistoryDescriptions
{
    static readonly Dictionary<string, string> Known = new()
    {
        ["AreaCreated"] = "Created an area",
        ["AreaRenamed"] = "Renamed an area",
        ["AreaDeleted"] = "Deleted an area",
        ["TaskListCreated"] = "Created a list",
        ["TaskListRenamed"] = "Renamed a list",
        ["TaskListMovedToArea"] = "Moved a list to another area",
        ["TaskListDeleted"] = "Deleted a list",
        ["TaskCreated"] = "Created a task",
        ["TaskRenamed"] = "Renamed a task",
        ["TaskDueDateSet"] = "Changed a task's due date",
        ["TaskPrioritySet"] = "Changed a task's priority",
        ["TaskStarred"] = "Changed a task's star",
        ["TaskLinkedToGoal"] = "Linked a task to a goal",
        ["TaskMovedToList"] = "Moved a task to another list",
        ["TaskCompleted"] = "Completed a task",
        ["TaskReopened"] = "Reopened a task",
        ["TaskDeleted"] = "Deleted a task",
        ["TaskRecurrenceSet"] = "Changed how a task repeats",
        ["OccurrenceCompleted"] = "Ticked a repeat",
        ["StepAdded"] = "Added a step",
        ["StepRenamed"] = "Renamed a step",
        ["StepDueDateSet"] = "Changed a step's due date",
        ["StepChecked"] = "Checked a step",
        ["StepsReordered"] = "Reordered steps",
        ["StepRemoved"] = "Removed a step",
        ["GoalCreated"] = "Created a goal",
        ["GoalRenamed"] = "Renamed a goal",
        ["GoalAchieved"] = "Achieved a goal",
        ["GoalReopened"] = "Reopened a goal",
        ["GoalDeleted"] = "Deleted a goal",
        ["InboxCreated"] = "Created the inbox",
        ["InboxItemCaptured"] = "Captured a thought",
        ["InboxItemOrganised"] = "Organised an inbox item into a task",
        ["InboxItemDiscarded"] = "Discarded an inbox item",
        ["UserProvisioned"] = "Signed in for the first time",
        ["UserTimeZoneSet"] = "Changed the time zone",
        ["UserDisplayNameSet"] = "Updated the display name from your account"
    };

    public static string For(string eventType) =>
        Known.TryGetValue(eventType, out var description) ? description : Humanise(eventType);

    static string Humanise(string eventType)
    {
        var spaced = WordBoundary().Replace(eventType, " $1").Trim();
        return char.ToUpperInvariant(spaced[0]) + spaced[1..].ToLowerInvariant();
    }

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex WordBoundary();
}
