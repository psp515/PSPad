namespace PSPad.Module.Reminders;

public interface IVapidKeyStore
{
    Task<VapidKeys> GetOrCreateAsync(Func<VapidKeys> generate, CancellationToken ct);
}
