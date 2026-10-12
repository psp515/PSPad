using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Goals;

public sealed record AddTaskToGoal(Guid CommandId, Guid UserId, Guid GoalId, Guid TaskId) : ICommand;
