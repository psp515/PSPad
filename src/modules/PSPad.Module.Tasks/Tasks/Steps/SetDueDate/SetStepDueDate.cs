using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record SetStepDueDate(Guid CommandId, Guid UserId, Guid TaskId, Guid StepId, DateOnly? DueOn)
    : ICommand;
