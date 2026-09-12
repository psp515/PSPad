namespace PSPad.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
