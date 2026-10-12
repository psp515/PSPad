namespace PSPad.Backup.Health;

public interface IPing
{
    Task PingAsync(bool succeeded, CancellationToken ct);
}
