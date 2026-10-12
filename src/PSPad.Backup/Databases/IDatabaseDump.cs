namespace PSPad.Backup.Databases;

public interface IDatabaseDump
{
    string ConfirmationWord { get; }

    Task DumpAsync(string directory, CancellationToken ct);

    Task RestoreAsync(string directory, CancellationToken ct);
}
