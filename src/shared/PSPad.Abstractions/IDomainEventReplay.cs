namespace PSPad.Abstractions;

public interface IDomainEventReplay
{
    Task CatchUpAsync(CancellationToken ct);
}
