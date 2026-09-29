using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record SetTaskDescription(Guid CommandId, Guid UserId, Guid TaskId, string Description) : ICommand;
