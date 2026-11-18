using PSPad.Abstractions;
using PSPad.Module.Reminders;

namespace PSPad.Api.Reminders;

public sealed class VapidKeyring(IServiceScopeFactory scopes, IClock clock) : IDisposable
{
    readonly SemaphoreSlim _gate = new(1, 1);
    VapidKeys? _keys;

    public async Task<VapidKeys> GetAsync(CancellationToken ct)
    {
        if (_keys is { } loaded)
        {
            return loaded;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_keys is null)
            {
                using var scope = scopes.CreateScope();
                _keys = await scope.ServiceProvider.GetRequiredService<IVapidKeyStore>()
                    .GetOrCreateAsync(() => VapidKeys.Generate(clock.UtcNow), ct);
            }

            return _keys;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
