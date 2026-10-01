using PSPad.Abstractions;
using PSPad.Module.Sharing.Ports;

namespace PSPad.Module.Sharing.Tests.Fakes;

public sealed class RecordingServerCommands : IServerCommands
{
    readonly List<ICommand> _received = [];

    public IReadOnlyList<ICommand> Received => _received;

    public string? RejectWith { get; set; }

    public Task<CommandResult> RunAsync(ICommand command, CancellationToken ct)
    {
        _received.Add(command);
        return Task.FromResult(RejectWith is { } reason ? CommandResult.Rejected(reason) : CommandResult.Ok());
    }
}
