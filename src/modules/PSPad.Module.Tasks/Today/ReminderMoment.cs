namespace PSPad.Module.Tasks.Today;

public sealed record ReminderMoment(DateOnly Occurrence, DateTimeOffset Moment);
