using Microsoft.JSInterop;

namespace PSPad.App.Tests.Sync;

public sealed class SilentJsRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        new(Task.FromException<TValue>(new InvalidOperationException("no interop in tests")));

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args) =>
        new(Task.FromException<TValue>(new InvalidOperationException("no interop in tests")));
}
