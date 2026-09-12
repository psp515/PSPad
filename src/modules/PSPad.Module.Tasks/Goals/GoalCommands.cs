using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Goals;

public sealed record CreateGoal(Guid CommandId, Guid UserId, Guid GoalId, string Name) : ICommand;

public sealed record RenameGoal(Guid CommandId, Guid UserId, Guid GoalId, string Name) : ICommand;

public sealed record AchieveGoal(Guid CommandId, Guid UserId, Guid GoalId) : ICommand;

public sealed record ReopenGoal(Guid CommandId, Guid UserId, Guid GoalId) : ICommand;

public sealed record DeleteGoal(Guid CommandId, Guid UserId, Guid GoalId) : ICommand;
