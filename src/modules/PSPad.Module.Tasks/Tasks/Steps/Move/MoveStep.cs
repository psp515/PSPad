using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record MoveStep(Guid CommandId, Guid UserId, Guid TaskId, Guid StepId, int ToIndex) : ICommand;
