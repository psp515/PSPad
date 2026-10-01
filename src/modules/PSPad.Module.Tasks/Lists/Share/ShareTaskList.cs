using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record ShareTaskList(Guid CommandId, Guid UserId, Guid ListId, string Token, string OwnerName) : ICommand;
