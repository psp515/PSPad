namespace PSPad.Module.Reminders.Tests.Fakes;

public sealed class FakeReminderSource(params ReminderCandidate[] candidates) : IReminderSource
{
    public Task<IReadOnlyList<ReminderCandidate>> CandidatesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ReminderCandidate>>(candidates);
}
