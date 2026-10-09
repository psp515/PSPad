using PSPad.Abstractions;

namespace PSPad.Module.Money.Tests.Fakes;

public sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; } = now;
}
