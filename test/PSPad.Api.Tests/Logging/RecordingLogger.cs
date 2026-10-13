using Microsoft.Extensions.Logging;

namespace PSPad.Api.Tests.Logging;

public sealed class RecordingLogger<T>(RecordedLogs logs) : ILogger<T>
{
    public RecordingLogger() : this(new RecordedLogs())
    {
    }

    public RecordedLogs Logs => logs;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        logs.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
}
