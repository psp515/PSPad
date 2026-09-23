using PSPad.Abstractions;
using PSPad.App.Sync;

namespace PSPad.App.State.Dispatch;

public sealed class CommandSender(IServiceProvider services, ReplicaUnitOfWork work, ISyncTrigger sync)
{
    public event Action? Sent;

    public async Task<CommandResult> SendAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : ICommand
    {
        work.Queue(command);
        var handler = services.GetService(typeof(ICommandHandler<TCommand>)) as ICommandHandler<TCommand>;

        if (handler is null)
        {
            return CommandResult.Rejected($"No handler for {typeof(TCommand).Name}.");
        }

        var result = await handler.HandleAsync(command, ct);

        if (result.Accepted)
        {
            Sent?.Invoke();
            _ = sync.SyncNowAsync();
        }

        return result;
    }
}
