namespace PSPad.Module.Tasks.Inbox;

public sealed record InboxItem(Guid Id, string Text, DateTimeOffset CapturedAt, int Position);
