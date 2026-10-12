namespace PSPad.Module.Sharing.Ports;

public interface IMarkBudget
{
    Task<bool> TryTakeAsync(Guid listId, DateOnly day, int limit, CancellationToken ct);
}
