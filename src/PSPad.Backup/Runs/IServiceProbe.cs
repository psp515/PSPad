namespace PSPad.Backup.Runs;

public interface IServiceProbe
{
    Task<bool> IsAcceptingAsync(string target, CancellationToken ct);
}
