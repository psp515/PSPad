using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Goals;

public sealed record ReopenGoal(Guid CommandId, Guid UserId, Guid GoalId) : ICommand;
