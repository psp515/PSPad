using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed record RemoveListMember(Guid CommandId, Guid UserId, Guid ListId, Guid MemberId) : ICommand;
