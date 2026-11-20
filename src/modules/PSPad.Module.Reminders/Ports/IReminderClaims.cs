namespace PSPad.Module.Reminders;

public interface IReminderClaims
{
    Task<bool> TryClaimAsync(ReminderClaim claim, CancellationToken ct);

    Task UpdateAsync(string id, ClaimState state, int attempts, CancellationToken ct);

    Task<IReadOnlyList<ReminderClaim>> RetriesSinceAsync(DateTimeOffset since, CancellationToken ct);

    Task PruneBeforeAsync(DateTimeOffset before, CancellationToken ct);
}
