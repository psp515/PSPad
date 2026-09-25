using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Goals;

public sealed record SetGoalStatus(Guid CommandId, Guid UserId, Guid GoalId, GoalStatus Status) : ICommand;
