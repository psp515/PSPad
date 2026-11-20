namespace PSPad.Module.Reminders;

public sealed record TickResult(int Sent, int Skipped, int Retried, int Failed);
