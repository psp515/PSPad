namespace PSPad.Module.Reminders;

public interface IReminderSource
{
    Task<IReadOnlyList<ReminderCandidate>> CandidatesAsync(CancellationToken ct);
}
