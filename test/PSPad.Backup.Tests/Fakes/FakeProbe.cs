using PSPad.Backup.Runs;

namespace PSPad.Backup.Tests.Fakes;

sealed class FakeProbe(bool accepting) : IServiceProbe
{
    public Task<bool> IsAcceptingAsync(string target, CancellationToken ct) => Task.FromResult(accepting);
}
