using PSPad.Backup.Databases;

namespace PSPad.Backup.Tests.Fakes;

sealed class FakeDump : IDatabaseDump
{
    public string Data { get; set; } = "rows";

    public bool FailDump { get; init; }

    public string? Restored { get; private set; }

    public string ConfirmationWord => "pspad";

    public Task DumpAsync(string directory, CancellationToken ct)
    {
        if (FailDump)
        {
            throw new BackupFailure("mongodump failed: connection refused");
        }

        return File.WriteAllTextAsync(Path.Combine(directory, "data.txt"), Data, ct);
    }

    public async Task RestoreAsync(string directory, CancellationToken ct) =>
        Restored = await File.ReadAllTextAsync(Path.Combine(directory, "data.txt"), ct);
}
