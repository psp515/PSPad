namespace PSPad.Module.Reminders;

public sealed record Recipient(Guid UserId, string TimeZone, string Language);
