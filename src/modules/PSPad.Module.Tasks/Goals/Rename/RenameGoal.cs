using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Goals;

public sealed record RenameGoal(Guid CommandId, Guid UserId, Guid GoalId, string Name) : ICommand;
