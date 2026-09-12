using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Inbox;

public sealed record DiscardInboxItem(Guid CommandId, Guid UserId, Guid InboxId, Guid ItemId) : ICommand;
