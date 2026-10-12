using PSPad.Backup.Health;

namespace PSPad.Backup.Tests.Fakes;

sealed class FakePing : IPing
{
    public List<bool> Sent { get; } = [];

    public Task PingAsync(bool succeeded, CancellationToken ct)
    {
        Sent.Add(succeeded);
        return Task.CompletedTask;
    }
}
