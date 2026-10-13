using Microsoft.Extensions.Logging;

namespace PSPad.Api.Tests.Logging;

public sealed record LogEntry(LogLevel Level, EventId Id, string Message);
