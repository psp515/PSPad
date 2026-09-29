using PSPad.Abstractions;

namespace PSPad.App.Tests;

public sealed class GatedHandler<TCommand> : ICommandHandler<TCommand> where TCommand : ICommand
{
    readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int Calls { get; private set; }

    public void Open() => _gate.TrySetResult();

    public async Task<CommandResult> HandleAsync(TCommand command, CancellationToken ct)
    {
        Calls++;
        await _gate.Task;
        return CommandResult.Ok();
    }
}
