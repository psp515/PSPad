using System.Net.Sockets;

namespace PSPad.Backup.Runs;

public sealed class TcpServiceProbe : IServiceProbe
{
    public async Task<bool> IsAcceptingAsync(string target, CancellationToken ct)
    {
        var separator = target.LastIndexOf(':');
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(target[..separator], int.Parse(target[(separator + 1)..]), timeout.Token);
            return true;
        }
        catch (Exception exception) when (exception is SocketException || (exception is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return false;
        }
    }
}
