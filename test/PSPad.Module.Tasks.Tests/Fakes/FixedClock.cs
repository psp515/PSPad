using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tests.Fakes;

public sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; } = now;
}
