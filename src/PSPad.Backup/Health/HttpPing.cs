namespace PSPad.Backup.Health;

public sealed class HttpPing(HttpClient http, string? url, BackupLog log) : IPing
{
    public async Task PingAsync(bool succeeded, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        try
        {
            using var response = await http.GetAsync(succeeded ? url : $"{url.TrimEnd('/')}/fail", ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            log.Info($"ping to BACKUP_PING_URL failed: {exception.Message}");
        }
    }
}
