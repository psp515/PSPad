using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Inbox;

public sealed record CaptureToInbox(Guid CommandId, Guid UserId, Guid InboxId, Guid ItemId, string Text)
    : ICommand;
