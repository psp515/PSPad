using System.Security.Cryptography;

namespace PSPad.Module.Reminders;

public sealed record VapidKeys(string PublicKey, string PrivateKey, DateTimeOffset CreatedAt)
{
    public static VapidKeys Generate(DateTimeOffset at)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(true);
        byte[] publicPoint = [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];
        return new VapidKeys(Base64Url(publicPoint), Base64Url(parameters.D!), at);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
