using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Goals;

public sealed record RemoveTaskFromGoal(Guid CommandId, Guid UserId, Guid GoalId, Guid TaskId) : ICommand;
