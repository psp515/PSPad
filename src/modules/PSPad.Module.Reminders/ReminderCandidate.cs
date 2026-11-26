using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Reminders;

public sealed record ReminderCandidate(TodoTask Task, IReadOnlyList<Recipient> Recipients);
