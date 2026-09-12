using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record RenameStep(Guid CommandId, Guid UserId, Guid TaskId, Guid StepId, string Name) : ICommand;
