using PSPad.Abstractions;
using PSPad.App.Sync;

namespace PSPad.App.State.Dispatch;

public sealed class CommandSender(IServiceProvider services, ReplicaUnitOfWork work, ISyncTrigger sync)
{
    readonly SemaphoreSlim _oneAtATime = new(1, 1);

    public event Action? Sent;

    public async Task<CommandResult> SendAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : ICommand
    {
        var handler = services.GetService(typeof(ICommandHandler<TCommand>)) as ICommandHandler<TCommand>;

        if (handler is null)
        {
            return CommandResult.Rejected($"No handler for {typeof(TCommand).Name}.");
        }

        CommandResult result;
        await _oneAtATime.WaitAsync(ct);
        try
        {
            work.Queue(command);
            result = await handler.HandleAsync(command, ct);
        }
        finally
        {
            work.Discard();
            _oneAtATime.Release();
        }

        if (result.Accepted)
        {
            Sent?.Invoke();
            _ = sync.SyncNowAsync();
        }

        return result;
    }
}
