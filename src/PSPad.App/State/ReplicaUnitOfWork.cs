using System.Text.Json;
using PSPad.Abstractions;
using PSPad.App.Api;
using PSPad.Contracts;

namespace PSPad.App.State;

public sealed class ReplicaUnitOfWork(IReplica replica, PSPadApiClient api) : IUnitOfWork
{
    readonly List<Aggregate> _staged = [];
    readonly List<CommandEnvelope> _pending = [];

    public IReadOnlyList<CommandResponse> LastResponses { get; private set; } = [];

    public void Stage(Aggregate aggregate, IReadOnlyList<DomainEvent> events) => _staged.Add(aggregate);

    public void Queue(ICommand command) =>
        _pending.Add(new CommandEnvelope(
            command.GetType().Name, JsonSerializer.SerializeToElement(command, command.GetType())));

    public async Task CommitAsync(Guid commandId, Guid userId, CancellationToken ct)
    {
        foreach (var aggregate in _staged)
        {
            await replica.SaveAsync(aggregate);
        }

        _staged.Clear();

        if (_pending.Count > 0)
        {
            LastResponses = await api.SendAsync(_pending);
            _pending.Clear();
        }
    }

    public Task<bool> IsProcessedAsync(Guid commandId, CancellationToken ct) => Task.FromResult(false);
}
