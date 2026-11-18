namespace PSPad.Module.Reminders;

public interface IPushSender
{
    Task<int?> SendAsync(PushSubscriptionRecord to, PushNote note, TimeSpan ttl, CancellationToken ct);
}
