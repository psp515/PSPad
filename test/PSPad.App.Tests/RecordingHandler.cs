using PSPad.Abstractions;

namespace PSPad.App.Tests;

public sealed class RecordingHandler<TCommand> : ICommandHandler<TCommand> where TCommand : ICommand
{
    public List<TCommand> Received { get; } = [];

    public Task<CommandResult> HandleAsync(TCommand command, CancellationToken ct)
    {
        Received.Add(command);
        return Task.FromResult(CommandResult.Ok());
    }
}
