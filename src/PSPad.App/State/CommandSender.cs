using PSPad.Abstractions;

namespace PSPad.App.State;

public sealed class CommandSender(IServiceProvider services, ReplicaUnitOfWork work)
{
    public async Task<CommandResult> SendAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : ICommand
    {
        work.Queue(command);
        var handler = services.GetService(typeof(ICommandHandler<TCommand>)) as ICommandHandler<TCommand>;

        return handler is null
            ? CommandResult.Rejected($"No handler for {typeof(TCommand).Name}.")
            : await handler.HandleAsync(command, ct);
    }
}
