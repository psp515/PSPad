using PSPad.App.Auth;

namespace PSPad.App.Tests.Auth;

public sealed class InMemoryLocalSessionStore(LocalSession? session = null) : ILocalSessionStore
{
    public LocalSession? Current { get; private set; } = session;

    public int Clears { get; private set; }

    public Action? Cleared { get; set; }

    public Task<LocalSession?> LoadAsync() => Task.FromResult(Current);

    public Task SaveAsync(LocalSession value)
    {
        Current = value;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Current = null;
        Clears++;
        Cleared?.Invoke();
        return Task.CompletedTask;
    }
}
