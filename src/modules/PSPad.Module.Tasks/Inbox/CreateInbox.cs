using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Inbox;

public sealed record CreateInbox(Guid CommandId, Guid UserId, Guid InboxId) : ICommand;
