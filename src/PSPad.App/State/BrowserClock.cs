using PSPad.Abstractions;

namespace PSPad.App.State;

public sealed class BrowserClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
