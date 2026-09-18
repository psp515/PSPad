using System.Text.Json;
using PSPad.Abstractions;
using PSPad.Contracts;
using PSPad.App.State.Outbox;

namespace PSPad.App.State;

public sealed class ReplicaUnitOfWork(IReplica replica, IOutbox outbox) : IUnitOfWork
{
    readonly List<Aggregate> _staged = [];
    readonly List<ICommand> _pending = [];

    public void Stage(Aggregate aggregate, IReadOnlyList<DomainEvent> events) => _staged.Add(aggregate);

    public void Queue(ICommand command) => _pending.Add(command);

    public async Task CommitAsync(Guid commandId, Guid userId, CancellationToken ct)
    {
        foreach (var aggregate in _staged)
        {
            await replica.SaveAsync(aggregate);
        }

        _staged.Clear();

        foreach (var command in _pending)
        {
            var envelope = new CommandEnvelope(
                command.GetType().Name, JsonSerializer.SerializeToElement(command, command.GetType()));
            await outbox.AppendAsync(commandId, envelope);
        }

        _pending.Clear();
    }

    public Task<bool> IsProcessedAsync(Guid commandId, CancellationToken ct) => Task.FromResult(false);
}
