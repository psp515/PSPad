using System.Security.Cryptography;
using PSPad.Module.Reminders;

namespace PSPad.Api.Tests.Reminders;

public sealed class BrowserDevice : IDisposable
{
    BrowserDevice(ECDiffieHellman key, byte[] auth, string endpoint)
    {
        Key = key;
        Auth = auth;
        Endpoint = endpoint;
        var point = key.ExportParameters(false).Q;
        PublicKey = [0x04, .. point.X!, .. point.Y!];
    }

    public ECDiffieHellman Key { get; }

    public byte[] PublicKey { get; }

    public byte[] Auth { get; }

    public string Endpoint { get; }

    public static BrowserDevice Create() =>
        new(ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256), RandomNumberGenerator.GetBytes(16),
            $"https://push.test/{Guid.NewGuid()}");

    public PushSubscriptionRecord RecordFor(Guid userId, DateTimeOffset at) =>
        new(PushSubscriptionRecord.IdFor(Endpoint), userId, Endpoint, Base64Url.Encode(PublicKey),
            Base64Url.Encode(Auth), "Test browser", at, null);

    public string Decrypt(PushRequest request) => PushPayloadDecryption.Decrypt(request.Body, Key, PublicKey, Auth);

    public void Dispose() => Key.Dispose();
}
