using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record RemoveStep(Guid CommandId, Guid UserId, Guid TaskId, Guid StepId) : ICommand;
