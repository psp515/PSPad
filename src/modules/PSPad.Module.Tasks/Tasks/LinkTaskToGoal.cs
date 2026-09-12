using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record LinkTaskToGoal(Guid CommandId, Guid UserId, Guid TaskId, Guid? GoalId) : ICommand;
