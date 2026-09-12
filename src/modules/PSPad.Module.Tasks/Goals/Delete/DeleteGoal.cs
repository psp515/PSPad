using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Goals;

public sealed record DeleteGoal(Guid CommandId, Guid UserId, Guid GoalId) : ICommand;
