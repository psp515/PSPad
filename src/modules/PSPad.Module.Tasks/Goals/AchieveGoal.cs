using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Goals;

public sealed record AchieveGoal(Guid CommandId, Guid UserId, Guid GoalId) : ICommand;
