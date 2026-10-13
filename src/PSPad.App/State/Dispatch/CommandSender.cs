using System.Reflection;
using System.Runtime.ExceptionServices;
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
        var result = await RunAsync(command, ct);

        if (result.Accepted)
        {
            Announce();
        }

        return result;
    }

    public async Task<IReadOnlyList<CommandResult>> SendManyAsync(
        IReadOnlyList<ICommand> commands, IProgress<int>? progress, CancellationToken ct)
    {
        var results = new List<CommandResult>(commands.Count);

        try
        {
            foreach (var command in commands)
            {
                ct.ThrowIfCancellationRequested();
                results.Add(await RunAsync(command, ct));
                progress?.Report(results.Count);
            }
        }
        finally
        {
            if (results.Any(result => result.Accepted))
            {
                Announce();
            }
        }

        return results;
    }

    async Task<CommandResult> RunAsync(ICommand command, CancellationToken ct)
    {
        var contract = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
        var handler = services.GetService(contract);

        if (handler is null)
        {
            return CommandResult.Rejected($"No handler for {command.GetType().Name}.");
        }

        await _oneAtATime.WaitAsync(ct);
        try
        {
            work.Queue(command);
            var handle = contract.GetMethod(nameof(ICommandHandler<ICommand>.HandleAsync))!;
            return await (Task<CommandResult>)handle.Invoke(handler, [command, ct])!;
        }
        catch (TargetInvocationException invocation) when (invocation.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(invocation.InnerException).Throw();
            throw;
        }
        finally
        {
            work.Discard();
            _oneAtATime.Release();
        }
    }

    void Announce()
    {
        Sent?.Invoke();
        _ = sync.SyncNowAsync();
    }
}
