namespace PSPad.Module.Reminders.Tests.Fakes;

public sealed class FakeReminderClaims : IReminderClaims
{
    public Dictionary<string, ReminderClaim> Claims { get; } = [];

    public DateTimeOffset? PrunedBefore { get; private set; }

    public Task<bool> TryClaimAsync(ReminderClaim claim, CancellationToken ct) =>
        Task.FromResult(Claims.TryAdd(claim.Id, claim));

    public Task UpdateAsync(string id, ClaimState state, int attempts, CancellationToken ct)
    {
        Claims[id] = Claims[id] with { State = state, Attempts = attempts };
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReminderClaim>> RetriesSinceAsync(DateTimeOffset since, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ReminderClaim>>(
            Claims.Values.Where(c => c.State == ClaimState.Retry && c.ClaimedAt >= since).ToList());

    public Task PruneBeforeAsync(DateTimeOffset before, CancellationToken ct)
    {
        PrunedBefore = before;
        return Task.CompletedTask;
    }
}
