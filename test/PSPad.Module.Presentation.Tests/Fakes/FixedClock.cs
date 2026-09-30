using PSPad.Abstractions;

namespace PSPad.Module.Presentation.Tests.Fakes;

public sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; } = now;
}
