using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PSPad.App.Tests.Live;

public sealed class RecordingLogger<T> : ILogger<T>
{
    readonly ConcurrentQueue<(LogLevel Level, Exception? Exception)> _entries = new();

    public IReadOnlyList<(LogLevel Level, Exception? Exception)> Entries => [.. _entries];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        _entries.Enqueue((logLevel, exception));
}
