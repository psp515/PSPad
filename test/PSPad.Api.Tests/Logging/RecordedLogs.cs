using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PSPad.Api.Tests.Logging;

public sealed class RecordedLogs : ILoggerProvider
{
    readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    public void Add(LogEntry entry) => _entries.Enqueue(entry);

    public ILogger CreateLogger(string categoryName) => new RecordingLogger<object>(this);

    public void Dispose()
    {
    }
}
