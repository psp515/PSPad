using System.Security.Cryptography;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Reminders.Tests;

[UnitTest]
public class VapidKeysTests
{
    private static byte[] Decode(string value) =>
        Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '='));

    [Fact]
    public void Generates_an_uncompressed_point_and_a_scalar()
    {
        var keys = VapidKeys.Generate(DateTimeOffset.UtcNow);

        var publicKey = Decode(keys.PublicKey);
        Assert.Equal(65, publicKey.Length);
        Assert.Equal(0x04, publicKey[0]);
        Assert.Equal(32, Decode(keys.PrivateKey).Length);
        Assert.DoesNotContain('=', keys.PublicKey + keys.PrivateKey);
    }

    [Fact]
    public void Each_call_makes_new_keys() =>
        Assert.NotEqual(VapidKeys.Generate(DateTimeOffset.UtcNow).PrivateKey, VapidKeys.Generate(DateTimeOffset.UtcNow).PrivateKey);

    [Fact]
    public void The_pair_signs_and_verifies()
    {
        var keys = VapidKeys.Generate(DateTimeOffset.UtcNow);
        var publicKey = Decode(keys.PublicKey);

        using var signer = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = Decode(keys.PrivateKey),
            Q = new ECPoint { X = publicKey[1..33], Y = publicKey[33..] },
        });
        var signature = signer.SignData("x"u8, HashAlgorithmName.SHA256);

        using var verifier = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = publicKey[1..33], Y = publicKey[33..] },
        });
        Assert.True(verifier.VerifyData("x"u8, signature, HashAlgorithmName.SHA256));
    }
}
