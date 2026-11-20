namespace PSPad.Module.Reminders;

public interface IPushSubscriptionStore
{
    Task<IReadOnlyList<PushSubscriptionRecord>> ForUserAsync(Guid userId, CancellationToken ct);

    Task<PushSubscriptionRecord?> FindAsync(string endpoint, CancellationToken ct);

    Task SaveAsync(PushSubscriptionRecord subscription, CancellationToken ct);

    Task DeleteAsync(string endpoint, CancellationToken ct);

    Task MarkDeliveredAsync(string endpoint, DateTimeOffset at, CancellationToken ct);
}
