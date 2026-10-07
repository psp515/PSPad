using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record JoinTaskList(Guid CommandId, Guid UserId, Guid ListId, string Token, string Code, string DisplayName)
    : IServerOnlyCommand;
