using System.Security.Cryptography;
using System.Text;

namespace PSPad.Api.Tests.Reminders;

public static class PushPayloadDecryption
{
    public static string Decrypt(byte[] body, ECDiffieHellman uaKey, byte[] uaPublic, byte[] auth)
    {
        var salt = body[..16];
        var idLength = body[20];
        var asPublic = body[21..(21 + idLength)];
        var cipher = body[(21 + idLength)..];
        using var server = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = asPublic[1..33], Y = asPublic[33..65] },
        });
        var shared = uaKey.DeriveRawSecretAgreement(server.PublicKey);
        var keyInfo = "WebPush: info\0"u8.ToArray().Concat(uaPublic).Concat(asPublic).ToArray();
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, auth, keyInfo);
        var cek = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, "Content-Encoding: aes128gcm\0"u8.ToArray());
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, "Content-Encoding: nonce\0"u8.ToArray());
        var plain = new byte[cipher.Length - 16];
        using (var aes = new AesGcm(cek, 16))
        {
            aes.Decrypt(nonce, cipher[..^16], cipher[^16..], plain);
        }

        var delimiter = Array.FindLastIndex(plain, b => b != 0);
        return Encoding.UTF8.GetString(plain, 0, delimiter);
    }
}
