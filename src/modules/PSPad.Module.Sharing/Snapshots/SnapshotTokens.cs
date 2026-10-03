using System.Security.Cryptography;

namespace PSPad.Module.Sharing.Snapshots;

public static class SnapshotTokens
{
    const int TokenBytes = 18;

    public static string New() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenBytes))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
