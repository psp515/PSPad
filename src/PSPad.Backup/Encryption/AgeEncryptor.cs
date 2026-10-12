using PSPad.Backup.Processes;

namespace PSPad.Backup.Encryption;

public sealed class AgeEncryptor(IProcessRunner runner, string? recipient) : IEncryptor
{
    public Task EncryptAsync(string source, string destination, CancellationToken ct) =>
        runner.RunAsync("age", ["-r", recipient ?? throw new BackupFailure("BACKUP_AGE_RECIPIENT is not set"), "-o", destination, source], null, ct);

    public Task DecryptAsync(string source, string destination, string identityFile, CancellationToken ct) =>
        runner.RunAsync("age", ["-d", "-i", identityFile, "-o", destination, source], null, ct);

    public async Task<bool> AcceptsRecipientAsync(CancellationToken ct)
    {
        try
        {
            await runner.RunAsync("age", ["-r", recipient!, "-o", "/dev/null"], null, ct);
            return true;
        }
        catch (BackupFailure)
        {
            return false;
        }
    }
}
