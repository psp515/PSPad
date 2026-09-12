using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record RenameTaskList(Guid CommandId, Guid UserId, Guid ListId, string Name) : ICommand;
