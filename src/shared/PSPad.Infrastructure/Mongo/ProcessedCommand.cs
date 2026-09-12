namespace PSPad.Infrastructure.Mongo;

public sealed class ProcessedCommand
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public DateTimeOffset At { get; init; }
}
