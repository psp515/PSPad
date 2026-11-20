using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PSPad.Api.Tests.Reminders;

public static class Vapid
{
    public static JsonElement Verify(string authorization, string expectedPublicKey)
    {
        Assert.StartsWith("vapid ", authorization);
        var parameters = authorization["vapid ".Length..]
            .Split(',', StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => pair[1]);
        Assert.Equal(expectedPublicKey, parameters["k"]);

        var parts = parameters["t"].Split('.');
        var point = Base64Url.Decode(parameters["k"]);
        using var key = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = point[1..33], Y = point[33..65] },
        });
        Assert.True(key.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Base64Url.Decode(parts[2]),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation),
            "The VAPID token's signature does not verify against the stored public key.");

        return JsonDocument.Parse(Base64Url.Decode(parts[1])).RootElement.Clone();
    }
}
