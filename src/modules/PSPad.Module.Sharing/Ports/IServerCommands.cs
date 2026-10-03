using PSPad.Abstractions;

namespace PSPad.Module.Sharing.Ports;

public interface IServerCommands
{
    Task<CommandResult> RunAsync(ICommand command, CancellationToken ct);
}
