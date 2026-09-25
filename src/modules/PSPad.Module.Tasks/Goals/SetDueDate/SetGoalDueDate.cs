using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Goals;

public sealed record SetGoalDueDate(Guid CommandId, Guid UserId, Guid GoalId, DateOnly? DueOn) : ICommand;
