using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record AddStep(Guid CommandId, Guid UserId, Guid TaskId, Guid StepId, string Name) : ICommand;
