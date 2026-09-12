using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Inbox;

public sealed record OrganiseInboxItem(
    Guid CommandId, Guid UserId, Guid InboxId, Guid ItemId, Guid ListId, Guid TaskId) : ICommand;
