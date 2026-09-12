using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record CheckStep(Guid CommandId, Guid UserId, Guid TaskId, Guid StepId, bool Checked) : ICommand;
