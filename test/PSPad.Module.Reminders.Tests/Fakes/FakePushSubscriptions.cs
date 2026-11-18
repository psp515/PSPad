namespace PSPad.Module.Reminders.Tests.Fakes;

public sealed class FakePushSubscriptions : IPushSubscriptionStore
{
    public Dictionary<string, PushSubscriptionRecord> Records { get; } = [];

    public void Add(Guid userId, string endpoint) =>
        Records[endpoint] = new PushSubscriptionRecord(
            PushSubscriptionRecord.IdFor(endpoint), userId, endpoint, "p", "a", "device",
            DateTimeOffset.UnixEpoch, null);

    public Task<IReadOnlyList<PushSubscriptionRecord>> ForUserAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PushSubscriptionRecord>>(
            Records.Values.Where(r => r.UserId == userId).ToList());

    public Task<PushSubscriptionRecord?> FindAsync(string endpoint, CancellationToken ct) =>
        Task.FromResult(Records.GetValueOrDefault(endpoint));

    public Task SaveAsync(PushSubscriptionRecord subscription, CancellationToken ct)
    {
        Records[subscription.Endpoint] = subscription;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string endpoint, CancellationToken ct)
    {
        Records.Remove(endpoint);
        return Task.CompletedTask;
    }

    public Task MarkDeliveredAsync(string endpoint, DateTimeOffset at, CancellationToken ct)
    {
        if (Records.TryGetValue(endpoint, out var record))
        {
            Records[endpoint] = record with { LastSuccessAt = at };
        }

        return Task.CompletedTask;
    }
}
