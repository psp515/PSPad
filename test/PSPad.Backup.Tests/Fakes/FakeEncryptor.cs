using System.Text;
using PSPad.Backup.Encryption;

namespace PSPad.Backup.Tests.Fakes;

sealed class FakeEncryptor : IEncryptor
{
    static readonly byte[] Marker = Encoding.ASCII.GetBytes("age-encryption.org/v1\n");

    public string? IdentityUsed { get; private set; }

    public async Task EncryptAsync(string source, string destination, CancellationToken ct) =>
        await File.WriteAllBytesAsync(destination, [.. Marker, .. await File.ReadAllBytesAsync(source, ct)], ct);

    public async Task DecryptAsync(string source, string destination, string identityFile, CancellationToken ct)
    {
        IdentityUsed = identityFile;
        var bytes = await File.ReadAllBytesAsync(source, ct);
        await File.WriteAllBytesAsync(destination, bytes[Marker.Length..], ct);
    }

    public static bool IsEncrypted(byte[] bytes) => bytes.AsSpan().StartsWith(Marker);
}
