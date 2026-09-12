using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Inbox;

public sealed record CreateInbox(Guid CommandId, Guid UserId, Guid InboxId) : ICommand;

public sealed record CaptureToInbox(Guid CommandId, Guid UserId, Guid InboxId, Guid ItemId, string Text)
    : ICommand;

public sealed record OrganiseInboxItem(
    Guid CommandId, Guid UserId, Guid InboxId, Guid ItemId, Guid ListId, Guid TaskId) : ICommand;

public sealed record DiscardInboxItem(Guid CommandId, Guid UserId, Guid InboxId, Guid ItemId) : ICommand;
