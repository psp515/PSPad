using PSPad.Module.Tasks.Tasks;

namespace PSPad.App.Components;

public sealed record AddTaskResult(
    string Name, DateOnly? DueOn, Priority Priority, Guid? GoalId, bool Starred);
