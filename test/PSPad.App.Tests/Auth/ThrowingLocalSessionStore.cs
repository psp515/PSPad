using PSPad.App.Auth;

namespace PSPad.App.Tests.Auth;

public sealed class ThrowingLocalSessionStore : ILocalSessionStore
{
    public Task<LocalSession?> LoadAsync() => throw new InvalidOperationException("IndexedDB unreadable");

    public Task SaveAsync(LocalSession session) => throw new InvalidOperationException("IndexedDB unreadable");

    public Task ClearAsync() => throw new InvalidOperationException("IndexedDB unreadable");
}
