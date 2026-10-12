using PSPad.Module.Sharing.Ports;

namespace PSPad.Module.Sharing.Tests.Fakes;

public sealed class InMemoryMarkBudget : IMarkBudget
{
    readonly Dictionary<(Guid ListId, DateOnly Day), int> _spent = [];

    public int SpentOn(Guid listId, DateOnly day) => _spent.GetValueOrDefault((listId, day));

    public void Spend(Guid listId, DateOnly day, int count) => _spent[(listId, day)] = count;

    public Task<bool> TryTakeAsync(Guid listId, DateOnly day, int limit, CancellationToken ct)
    {
        var spent = SpentOn(listId, day);
        if (spent >= limit)
        {
            return Task.FromResult(false);
        }

        _spent[(listId, day)] = spent + 1;
        return Task.FromResult(true);
    }
}
