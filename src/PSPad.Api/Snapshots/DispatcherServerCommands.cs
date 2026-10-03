using PSPad.Abstractions;
using PSPad.Api.Commands;
using PSPad.Module.Sharing.Ports;

namespace PSPad.Api.Snapshots;

public sealed class DispatcherServerCommands(CommandDispatcher dispatcher) : IServerCommands
{
    public Task<CommandResult> RunAsync(ICommand command, CancellationToken ct) => dispatcher.RunAsync(command, ct);
}
