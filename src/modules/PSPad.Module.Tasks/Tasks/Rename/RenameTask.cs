using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record RenameTask(Guid CommandId, Guid UserId, Guid TaskId, string Name) : ICommand;
