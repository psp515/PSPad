namespace PSPad.Backup.Encryption;

public interface IEncryptor
{
    Task EncryptAsync(string source, string destination, CancellationToken ct);

    Task DecryptAsync(string source, string destination, string identityFile, CancellationToken ct);
}
